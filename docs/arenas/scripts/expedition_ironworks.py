# Expedition CITY IRONWORKS instance, blockout DRAFT (owner-approved design 2026-10-16, NOT LIVE): map `expedition-ironworks`
# (the id tools/generate_expedition.py's draft table, NativeMapRules.ExpeditionInstance and NativeMachineRules use since PR #254;
# docs/EXPEDITION-LAYOUT.md first reserved `expedition-foundry`), area `exp-foundry` (the reserved area id and the town's sealed
# foundry plot at (-84, 0, 0); -108 since exp-layout-7). Design: docs/EXPEDITION-CITY-IRONWORKS.md. Nothing at runtime reads this script's outputs:
#   Tests/expedition-ironworks-draft.json         the map entry (UnityMaps.json shape, nav left to the importer)
#   Tests/expedition-ironworks-layout-draft.json  a manifest draft: the live exp-layout-3 manifest plus the town's foundry door
#                                                 and the exp-foundry area (Astra's parser needs the paired town door)
#   Tests/expedition-ironworks-look-draft.json    a MapLooks.json-shape look ("ironworks"), not in Resources/MapLooks.json
# Resources/UnityMaps.json, ExpeditionLayout.json, MapLooks.json and PveContent.json are read, never written.
# The geometry follows Ward Zero's instance conventions (expedition_ward_zero.py): an exit door in the shell with eight entry
# slots inside it, caches as `chest` anchors with an interaction 0.8 m in front, boss points, and its own spawn records in the
# PveEntry shape plus `allow` (doors, vents, drops, Hulk breaches; existing kinds only, so the strict parser accepts them).
# The direct converter shutter is a real instance gate (`gates[]`, opens "boss": PR #254's instance shutter, sealed until the
# instance's boss encounter starts). Everything the schema has no field for (rooms, proposed hazards and their wave schedule,
# the approved machine spots, the proposed new enemy kinds per spawn and the Rigger's hang points, the boss arena volume, the
# zone's presentation tints) is the additive `ironworks` record, which the loader ignores, as it ignores the hub record.
# Checks (static analysis; no Unity or PhysX claim): both floors reached from the entry slots, every record on walkable floor
# with pilot or Hulk clearance, every spawn exit on reached floor with its clear radius and routed back to entry 0 with its own
# capsule, no two solid blocks overlapping, no pilot exclusion column under a deck, stairs within the ramp rule, spawn spacing
# and walking distances, hazard proposals clear of records and their Wave 1 / boss schedule, the converter shutter filling its
# opening with every boss point and spawn exit still routed while it is shut, the Rigger's hang points (a girder overhead, the
# chain's fall clear, the landing ring on reached floor away from hazards), the machine spots, the town door's returns Hulk-clear
# and routed in the live town.
# Usage: python3 docs/arenas/scripts/expedition_ironworks.py [--fixtures] [--map m.json] [--layout l.json] [--look k.json]
#        [--png plan.png] [--ascii]
# Coordinates: the instance's own frame, x east, z north, y up, metres; floors at y 0 and 6. Deterministic.
import argparse, json, math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from expedition_common import (Collider, Canvas, text_width, components, block_hash, id_ok, v, q, vol, vol_floor, pt, vol_block, in_vol, vol_in_bounds, yaw_of,
                               yaw_rad, forward, dist_xz, yspan, footprint, corners, ramp_block, r4, RADIUS, HEIGHT, PILOT_CLEAR, HULK_CLEAR, HULK_BAND,
                               HULK_RADIUS, HULK_HEIGHT, HULK_SLOT)

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..'))
UNITY = os.path.join(ROOT, 'unity', 'ChipArenaConversion')
RES = os.path.join(UNITY, 'Assets', 'Conversion', 'Resources')
TESTS = os.path.join(UNITY, 'Tests')
FIXTURES = {'map': 'expedition-ironworks-draft.json', 'layout': 'expedition-ironworks-layout-draft.json', 'look': 'expedition-ironworks-look-draft.json'}
MAP = 'expedition-ironworks'; AREA = 'exp-foundry'; LOOK = 'ironworks'
REVISION = 'exp-layout-3-ironworks-draft'      # the draft manifest's revision (never a live revision)
LIVE_REVISION = 'exp-layout-3'                 # the live manifest the draft extends
X0, X1, Z0, Z1 = -50.0, 50.0, -34.0, 34.0      # 100 x 68 m inside the shell (Ward Zero: 76 x 60)
CEIL, KILL, L1 = 18.0, -6.0, 6.0               # roof underside, kill plane, the deck level
WALL_T, WALL_H, SHELL_H = 0.6, 14.0, 18.0      # partitions (open above 14 m under the roof), the shell to the roof
DECK_T = 0.3                                    # deck slabs y 5.7..6.0
DOOR_W, DOOR_H = 3.2, 3.4                       # the exit doorway (the closure's size, as Ward Zero's)
DOOR = (50.0, 0.0); YAW = 90.0                  # the exit in the east shell face (x 50), facing out (+x: toward the town, whose door faces +x)

blocks, ramps, drops_hint = [], [], []
def _add(b, ch, pilot, room):
    b['_ch'] = ch; b['_pilot'] = pilot; b['_room'] = room; blocks.append(b); return b
def box(id, x0, x1, z0, z1, y0, y1, material='stone', ch='#', pilot=False, room='', role='static'):
    """An axis-aligned static block from its min/max corners. `pilot`: a pilot exclusion column over its top (perches the
    horde cannot reach are kept off)."""
    assert x1 > x0 + 1e-9 and z1 > z0 + 1e-9 and y1 > y0 + 1e-9, id
    return _add(dict(id=MAP + '-' + id, material=material, pos=v((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), size=v(x1 - x0, y1 - y0, z1 - z0),
                     rotation=q(), role=role, mass=26, segment=None, hp=0), ch, pilot, room)
def cbox(id, cx, cz, sx, sz, y0, y1, **kw): return box(id, cx - sx / 2, cx + sx / 2, cz - sz / 2, cz + sz / 2, y0, y1, **kw)
def strips(u0, u1, h, openings, y0=0.0):
    """A face u0..u1 x y0..h minus opening rects (a, b, lo, hi): its solid pieces (a, b, v0, v1), merged along u."""
    cuts = sorted({u0, u1, *[o[0] for o in openings], *[o[1] for o in openings]}); out = []
    for a, b in zip(cuts, cuts[1:]):
        mid = (a + b) / 2; y = y0
        for lo, hi in sorted((o[2], o[3]) for o in openings if o[0] <= mid <= o[1]):
            if lo > y: out.append([a, b, y, lo])
            y = max(y, hi)
        if y < h: out.append([a, b, y, h])
    merged = []
    for p in out:
        if merged and abs(merged[-1][1] - p[0]) < 1e-6 and merged[-1][2:] == p[2:]: merged[-1][1] = p[1]
        else: merged.append(p)
    return merged
def wall_x(id, x, z0, z1, h=WALL_H, openings=(), material='brick', room='', y0=0.0):
    """A partition centred on x = x, from z0 to z1, with openings (za, zb, ylo, yhi)."""
    for k, (a, b, lo, hi) in enumerate(strips(z0, z1, h, list(openings), y0)): box(f'{id}-{k}', x - WALL_T / 2, x + WALL_T / 2, a, b, lo, hi, material=material, ch='|', room=room)
def wall_z(id, z, x0, x1, h=WALL_H, openings=(), material='brick', room='', y0=0.0):
    for k, (a, b, lo, hi) in enumerate(strips(x0, x1, h, list(openings), y0)): box(f'{id}-{k}', a, b, z - WALL_T / 2, z + WALL_T / 2, lo, hi, material=material, ch='-', room=room)
decks = []
def deck(id, x0, x1, z0, z1, room='', cols=None, rails=()):
    """A walkable deck at L1 (slab 5.7..6.0) on 0.5 m columns at `cols` [(x, z)], and 1.05 m guard rails along the named
    open edges ('n', 's', 'e', 'w'; each (side, from, to) leaves the rest of that edge open for drops)."""
    b = box(f'deck-{id}', x0, x1, z0, z1, L1 - DECK_T, L1, material='metal', ch='=', room=room); decks.append(b)
    for k, (cx, cz) in enumerate(cols or []): cbox(f'deck-{id}-col-{k}', cx, cz, 0.5, 0.5, 0, L1 - DECK_T, material='metal', ch='o', room=room)
    for k, (side, a, c) in enumerate(rails):
        if side in 'ns': z = z1 - 0.05 if side == 'n' else z0 + 0.05; box(f'deck-{id}-rail-{k}', a, c, z - 0.04, z + 0.04, L1, L1 + 1.05, material='metal', ch='r', room=room)
        else: x = x1 - 0.05 if side == 'e' else x0 + 0.05; box(f'deck-{id}-rail-{k}', x - 0.04, x + 0.04, a, c, L1, L1 + 1.05, material='metal', ch='r', room=room)
    return b
def stair(id, lx, lz, hx, hz, width=3.0, room=''):
    """A 6 m flight of 24 steps (0.25 m rise, 0.5 m run, solid to the floor as Siren's) and its walkable wedge (ramps[])."""
    L = math.hypot(hx - lx, hz - lz); n = 24; assert abs(L - 12.0) < 1e-6, id
    ux, uz = (hx - lx) / L, (hz - lz) / L; ids = []
    for k in range(n):
        a, b = k * L / n, (k + 1) * L / n; cx, cz = lx + ux * (a + b) / 2, lz + uz * (a + b) / 2
        sx, sz = (width, b - a) if abs(uz) > 0.5 else (b - a, width)
        ids.append(cbox(f'stair-{id}-{k:02d}', cx, cz, sx, sz, 0, (k + 1) * L1 / n, material='metal', ch='/', room=room)['id'])
    ramps.append(dict(id=f'{MAP}-stair-{id}', kind='stair', low=v(lx, 0, lz), high=v(hx, L1, hz), width=r4(width), bottom=0.0, blocks=ids))
def drop_hint(id, ax, az, bx, bz): drops_hint.append(dict(id=f'{MAP}-drop-{id}', a=v(ax, L1, az), b=v(bx, 0, bz)))

# =====================================================================================================================
# SHELL: the floor slab, four outer walls standing outside the bounds (Clamp keeps pilots inside, as Ward Zero's shell),
# the roof under the ceiling. The east wall carries the exit doorway's frame (the shell stays solid behind it).
box('slab', X0 - 1, X1 + 1, Z0 - 1, Z1 + 1, -0.5, 0, material='foundation', ch='.')
box('shell-n', X0 - 1, X1 + 1, Z1, Z1 + 1, 0, SHELL_H, ch='#'); box('shell-s', X0 - 1, X1 + 1, Z0 - 1, Z0, 0, SHELL_H, ch='#')
box('shell-e', X1, X1 + 1, Z0, Z1, 0, SHELL_H, ch='#'); box('shell-w', X0 - 1, X0, Z0, Z1, 0, SHELL_H, ch='#')
box('roof', X0 - 1, X1 + 1, Z0 - 1, Z1 + 1, SHELL_H, SHELL_H + 0.6, material='metal', ch='^')
FACE, DEPTH = X1, 0.5
dz0, dz1 = DOOR[1] - DOOR_W / 2, DOOR[1] + DOOR_W / 2
box('exit-jamb-s', FACE - DEPTH, FACE, dz0 - 0.4, dz0, 0, DOOR_H + 0.4, material='metal', ch='D', room='time-office')
box('exit-jamb-n', FACE - DEPTH, FACE, dz1, dz1 + 0.4, 0, DOOR_H + 0.4, material='metal', ch='D', room='time-office')
box('exit-lintel', FACE - DEPTH, FACE, dz0, dz1, DOOR_H, DOOR_H + 0.4, material='metal', ch='D', room='time-office')

# =====================================================================================================================
# PARTITIONS. Two long lines split the plan into a west wing (x < -24: coke ovens, converter hall, slag yard), the main hall
# (x -24..36: the furnace aisle, the casting floor, the rolling mill) and an east wing (x > 36: stockhouse, time office,
# pattern shop). Openings are 4-8 m wide and 5 m tall on the ground (a Hulk's 1.91 m with room to spare), and 3.5 m tall at
# the deck where the upper ring passes.
GO = 5.0                      # ground openings' height
DO = (L1, L1 + 3.5)           # deck-level openings' span
wall_x('wall-w', -24.0, Z0, Z1, openings=[(-30.5, -24.5, 0, GO), (-4.0, 4.0, 0, GO), (15.5, 20.5, 0, GO), (22.0, 28.0) + DO], room='')
wall_x('wall-e', 36.0, Z0, Z1, openings=[(-20.0, -16.0, 0, GO), (-4.0, 4.0, 0, GO), (24.5, 28.0, 0, GO), (22.0, 28.0) + DO], room='')
# The casting floor / rolling mill line: a wall under the south gallery (to the deck's underside) with two ground openings.
wall_z('wall-mill', -14.0, -23.7, 35.7, h=L1 - DECK_T, openings=[(1.0, 8.0, 0, GO), (23.0, 27.5, 0, GO)], room='')
# West wing rooms: the converter hall between the coke battery's bench and the slag yard.
wall_z('wall-conv-n', 12.0, X0, -24.3, openings=[(-35.0, -30.0, 0, GO)], room='converter-hall')
wall_z('wall-conv-s', -12.0, X0, -24.3, openings=[(-35.0, -30.0, 0, GO)], room='converter-hall')
# East wing rooms: the time office (entry) between the stockhouse and the pattern shop.
wall_z('wall-office-n', 10.0, 36.3, X1, openings=[(40.0, 44.0, 0, GO)], room='time-office')
wall_z('wall-office-s', -10.0, 36.3, X1, openings=[(40.0, 44.0, 0, GO)], room='time-office')

# =====================================================================================================================
# THE UPPER RING AT y 6 round the casting floor (the north charging deck, the east and west catwalks, the south gallery
# over the mill wall) and its branches: the coke battery walkway (west), the ore bridge (east). Columns every ~6 m.
deck('north', -23.7, 35.7, 22.0, 28.0, room='furnace-aisle', cols=[(x, 22.4) for x in (-18.0, -11.0, -5.0, 4.0, 10.0, 19.0, 25.0, 31.0)],
     rails=[('s', -19.7, -14.0), ('s', -8.0, 6.0), ('s', 12.0, 31.7)])
deck('east', 31.7, 35.7, -16.0, 22.0, room='casting-floor', cols=[(32.1, z) for z in (-9.0, -3.0, 3.0, 9.0)], rails=[('w', -12.0, 18.0)])
deck('west', -23.7, -19.7, -16.0, 22.0, room='casting-floor', cols=[(-20.1, z) for z in (-10.0, 4.0, 10.0, 16.0)], rails=[('e', -12.0, -5.0), ('e', -2.0, 6.0), ('e', 12.0, 22.0)])
deck('south', -19.7, 31.7, -16.0, -12.0, room='casting-floor', cols=[], rails=[('n', -19.7, -2.0), ('n', 10.0, 31.7), ('s', -19.7, 6.0), ('s', 21.0, 31.7)])
deck('coke', -50.0, -24.3, 22.0, 28.0, room='coke-ovens', cols=[(x, 22.4) for x in (-30.0, -36.0, -46.5)], rails=[('s', -40.0, -24.3)])
deck('ore', 40.0, 50.0, 22.0, 28.0, room='stockhouse', cols=[(44.0, 22.4), (48.0, 22.4)], rails=[('s', 40.0, 50.0)])
deck('ore-landing', 36.3, 40.0, 25.0, 28.0, room='stockhouse', cols=[], rails=[])
deck('cast-e-landing', 28.0, 31.7, 18.0, 22.0, room='casting-floor', cols=[], rails=[('w', 18.0, 22.0)])
deck('cast-w-landing', -19.7, -16.0, -5.0, -2.0, room='casting-floor', cols=[(-16.4, -4.6)], rails=[('s', -19.7, -16.0)])
deck('mill-landing', 18.0, 21.0, -21.0, -16.0, room='rolling-mill', cols=[], rails=[('e', -21.0, -16.0)])
deck('coke-landing', -44.0, -40.0, 17.0, 22.0, room='coke-ovens', cols=[(-43.6, 17.4)], rails=[('w', 17.0, 22.0)])
stair('stock', 38.5, 13.0, 38.5, 25.0, room='stockhouse')            # the stockhouse floor up to the ore bridge
stair('cast-e', 29.85, 6.0, 29.85, 18.0, width=3.7, room='casting-floor')    # the casting floor's north-east corner up to the ring
stair('cast-w', -17.85, 10.0, -17.85, -2.0, width=3.7, room='casting-floor') # and to the west catwalk
stair('mill', 6.0, -19.5, 18.0, -19.5, room='rolling-mill')         # the mill's north lane up to the gallery
stair('coke', -28.0, 18.5, -40.0, 18.5, room='coke-ovens')          # the coke bench up to the battery walkway
for k, (ax, az, bx, bz) in enumerate([(-11.0, 21.7, -11.0, 19.4), (10.0, 21.7, 10.0, 19.6), (-19.9, 11.0, -18.0, 11.0),
                                       (4.0, -11.9, 4.0, -10.2), (12.0, -16.1, 12.0, -17.3), (-46.0, 21.9, -46.0, 20.0)]):
    drop_hint(f'edge-{k}', ax, az, bx, bz)

# =====================================================================================================================
# ROOMS. Masses whose tops a pilot could reach but the horde could not carry exclusion columns (`pilot=True`).
# --- FURNACE AISLE (north of the casting floor, under and along the charging deck): three furnaces in the north wall, their
#     mouths on the deck (spawns walk out of the fire), their tapholes on the ground feeding the iron runners south.
box('flue-wall', -23.7, 35.7, 30.0, Z1, 0, WALL_H, ch='#', room='furnace-aisle')
for k, (x0, x1) in enumerate([(-20.0, -10.0), (-5.0, 5.0), (10.0, 20.0)]):
    box(f'furnace-{k + 1}', x0, x1, 28.0, 30.0, 0, WALL_H, material='brick', ch='F', room='furnace-aisle')
box('skip-housing', 26.0, 35.7, 28.0, 30.0, 0, WALL_H, material='metal', ch='#', room='furnace-aisle')   # the skip incline's foot
box('aisle-bay-w', -23.7, -20.0, 28.0, 30.0, 0, WALL_H, material='brick', ch='#', room='furnace-aisle')
# Iron runners (tapholes to the casting floor): 0.3 m curbs either side of a 1.2 m trough; the molten metal is the hazard's.
RUNNERS = [('r1', -15.0, 12.0), ('r2', 0.0, 12.0), ('r3', 15.0, 12.0)]       # (name, axis x, the trough's south end z); north end the furnace face
for name, ax, bz in RUNNERS:
    box(f'runner-{name}-curb-w', ax - 0.85, ax - 0.6, bz, 28.0, 0, 0.3, material='brick', ch='~', room='furnace-aisle')
    box(f'runner-{name}-curb-e', ax + 0.6, ax + 0.85, bz, 28.0, 0, 0.3, material='brick', ch='~', room='furnace-aisle')
    box(f'runner-{name}-curb-end', ax - 0.85, ax + 0.85, bz - 0.25, bz, 0, 0.3, material='brick', ch='~', room='casting-floor')
box('tap-cart-w', -9.0, -6.5, 15.0, 18.0, 0, 1.2, material='metal', ch='c', room='furnace-aisle')
box('tap-cart-e', 6.5, 9.0, 16.0, 19.0, 0, 1.2, material='metal', ch='c', room='furnace-aisle')
box('scrap-box-w', -23.0, -21.0, 8.0, 11.0, 0, 1.3, material='metal', ch='c', room='casting-floor')
box('scrap-box-e', 22.5, 25.0, 14.5, 17.0, 0, 1.3, material='metal', ch='c', room='furnace-aisle')
box('charge-box-0', -14.0, -11.5, 25.0, 27.0, L1, L1 + 1.2, material='metal', ch='c', room='furnace-aisle')   # on the deck
box('charge-box-1', 22.0, 24.5, 25.0, 27.0, L1, L1 + 1.2, material='metal', ch='c', room='furnace-aisle')

# --- CASTING FLOOR (the big arena under the crane): the torpedo cars on their track, ladles on stands, ingot mold rows,
#     the pig-casting bed. The crane bridge and its runway girders hang above reach.
box('torpedo-1', -13.0, -3.0, 1.0, 4.0, 0, 3.6, material='metal', ch='T', pilot=True, room='casting-floor')
box('torpedo-2', 9.0, 19.0, 1.0, 4.0, 0, 3.6, material='metal', ch='T', pilot=True, room='casting-floor')
box('ladle-stand-w', -12.0, -8.0, -10.0, -6.0, 0, 3.4, material='metal', ch='L', pilot=True, room='casting-floor')
box('ladle-stand-e', 25.5, 29.5, -11.9, -7.9, 0, 3.4, material='metal', ch='L', pilot=True, room='casting-floor')
for k, (x0, x1, z0, z1) in enumerate([(-1.0, 4.0, -8.0, -6.8), (6.0, 11.0, -8.0, -6.8), (-1.0, 4.0, -4.6, -3.4), (6.0, 11.0, -4.6, -3.4)]):
    box(f'mold-row-{k}', x0, x1, z0, z1, 0, 0.9, material='metal', ch='m', room='casting-floor')
box('pig-bed', 22.0, 28.0, 8.0, 12.0, 0, 0.7, material='metal', ch='m', room='casting-floor')
box('skull-pile', -17.0, -14.0, -13.2, -11.4, 0, 1.4, material='metal', ch='s', room='casting-floor')
box('crane-runway-n', -23.7, 35.7, 20.6, 21.4, 12.4, 13.2, material='metal', ch='-', room='casting-floor')
box('crane-runway-s', -23.7, 35.7, -12.4, -11.6, 12.4, 13.2, material='metal', ch='-', room='casting-floor')
box('crane-bridge', 4.0, 7.0, -12.4, 21.4, 13.2, 14.4, material='metal', ch='-', room='casting-floor')
box('crane-trolley', 3.6, 7.4, -7.5, -4.5, 12.0, 13.2, material='metal', ch='-', room='casting-floor')

# --- ROLLING MILL (the long south hall): the reheat furnace at the east end, the roller table down the hall through four
#     stands, the flying shear and the cooling bed at the west end; two lanes either side of the table.
box('reheat-furnace', 29.0, 35.7, -29.0, -20.0, 0, 9.0, material='brick', ch='F', room='rolling-mill')
# The table runs through the stands' windows (crossing aisles where it stops: x -4..0.1 and 20..23). Each stand is two
# 4.4 m housings either side of the table and a lintel 1.6 m over it: no way through for a pilot, but the eye follows the
# slab line down the hall, and the gallery at 6 m looks down over the stands onto it.
TABLE = [(-20.0, -4.0), (0.1, 20.0), (23.0, 29.0)]
for k, (x0, x1) in enumerate(TABLE): box(f'roller-table-{k}', x0, x1, -26.2, -23.8, 0, 0.9, material='metal', ch='=', room='rolling-mill')
def housing(id, x0, x1, top):
    box(f'{id}-n', x0, x1, -23.8, -21.0, 0, top, material='metal', ch='S', pilot=True, room='rolling-mill')
    box(f'{id}-s', x0, x1, -29.0, -26.2, 0, top, material='metal', ch='S', pilot=True, room='rolling-mill')
    box(f'{id}-lintel', x0, x1, -26.2, -23.8, 2.5, top, material='metal', ch='S', pilot=True, room='rolling-mill')
for k, x in enumerate((15.0, 8.0, 1.5, -10.0)): housing(f'mill-stand-{k}', x - 1.4, x + 1.4, 4.4)
box('mill-drive', 9.4, 13.6, -23.8, -21.0, 0, 1.0, material='metal', ch='m', room='rolling-mill')   # the stand drives between stands 0 and 1
housing('shear-housing', -16.5, -14.5, 5.0)
box('cooling-bed', -22.0, -17.0, -33.2, -29.0, 0, 0.6, material='metal', ch='m', room='rolling-mill')
box('pulpit', -6.5, -1.5, -33.4, -30.6, 0, 3.0, material='metal', ch='P', pilot=True, room='rolling-mill')
for k, (x0, x1) in enumerate([(5.0, 8.0), (20.0, 23.0)]): box(f'coil-stack-{k}', x0, x1, -33.4, -31.4, 0, 1.6, material='metal', ch='o', room='rolling-mill')
box('billet-rack', 30.0, 34.0, -18.6, -17.4, 0, 1.2, material='metal', ch='o', room='rolling-mill')

# --- STOCKHOUSE (north-east): ore bunkers along the north wall over the ore bridge, a conveyor gallery, the stair.
box('bunkers', 36.3, X1, 30.0, Z1, 0, WALL_H, material='metal', ch='#', room='stockhouse')
box('conveyor-frame', 42.0, 48.0, 14.0, 15.4, 0, 1.4, material='metal', ch='=', room='stockhouse')
box('ore-cart', 43.0, 45.5, 18.0, 20.5, 0, 1.4, material='metal', ch='c', room='stockhouse')

# --- TIME OFFICE (the entry, east): lockers, the clock rack and the shift board stopped at 6:47; the first cache.
box('lockers-n', 45.5, 49.5, 8.4, 9.6, 0, 2.2, material='metal', ch='l', pilot=True, room='time-office')
box('clock-rack', 40.5, 41.0, -9.6, -7.6, 0, 1.8, material='metal', ch='l', pilot=True, room='time-office')
box('bench', 44.5, 47.5, 5.6, 6.4, 0, 0.5, material='wood', ch='b', room='time-office')

# --- PATTERN SHOP (south-east): pattern racks, mold boxes, the sand muller. The proposed machine room (chip, med).
box('pattern-rack-0', 37.0, 38.2, -32.5, -24.0, 0, 2.6, material='wood', ch='R', pilot=True, room='pattern-shop')
box('pattern-rack-1', 41.0, 47.0, -21.5, -20.5, 0, 2.6, material='wood', ch='R', pilot=True, room='pattern-shop')
box('mold-boxes', 40.0, 43.0, -31.0, -28.0, 0, 1.2, material='wood', ch='b', room='pattern-shop')
box('sand-muller', 40.5, 43.5, -15.0, -12.0, 0, 1.5, material='metal', ch='M', pilot=True, room='pattern-shop')

# --- COKE OVENS (north-west): the battery along the north wall (oven doors on its face), the pusher car parked on the bench,
#     the quench tower at the west end, coal hoppers. The walkway on the battery's face is the deck.
box('battery', X0, -24.3, 28.0, Z1, 0, WALL_H, material='brick', ch='B', room='coke-ovens')
box('quench-tower', X0, -45.0, 12.3, 16.0, 0, WALL_H, material='brick', ch='Q', room='coke-ovens')
box('pusher-car', -38.5, -35.5, 24.5, 27.6, 0, 3.6, material='metal', ch='P', room='coke-ovens')
box('hopper-0', -32.0, -30.0, 14.0, 16.0, 0, 1.3, material='metal', ch='c', room='coke-ovens')
box('hopper-1', -42.0, -40.0, 13.5, 15.5, 0, 1.3, material='metal', ch='c', room='coke-ovens')

# --- CONVERTER HALL (west, the boss arena placeholder): two Bessemer converters on their trunnions flank a pouring pit;
#     three ways in (the casting floor's west opening, the coke bench, the slag yard). No boss design yet (owner: later).
box('converter-n', -48.0, -42.0, 4.0, 10.0, 0, 7.5, material='metal', ch='C', pilot=True, room='converter-hall')
box('converter-s', -48.0, -42.0, -10.0, -4.0, 0, 7.5, material='metal', ch='C', pilot=True, room='converter-hall')
box('conv-crane-runway-e', -27.0, -26.2, -11.7, 11.7, 12.4, 13.2, material='metal', ch='-', room='converter-hall')
box('conv-crane-runway-w', -49.8, -49.0, -11.7, 11.7, 12.4, 13.2, material='metal', ch='-', room='converter-hall')

# --- SLAG YARD AND DROP FORGE (south-west): slag pot cars at the dump, the slag bank, the drop hammer over its anvil.
box('slag-bank', X0, -44.0, -33.7, -15.0, 0, 2.0, material='stone', ch='%', pilot=True, room='slag-yard')
box('slag-pot-0', -43.0, -39.5, -31.0, -27.5, 0, 3.0, material='metal', ch='o', pilot=True, room='slag-yard')
box('slag-pot-1', -43.0, -39.5, -21.0, -17.5, 0, 3.0, material='metal', ch='o', pilot=True, room='slag-yard')
box('hammer-col-w', -33.0, -31.8, -29.0, -27.8, 0, 9.0, material='metal', ch='H', room='slag-yard')
box('hammer-col-e', -27.2, -26.0, -29.0, -27.8, 0, 9.0, material='metal', ch='H', room='slag-yard')
box('hammer-head', -33.0, -26.0, -29.0, -27.8, 9.0, 10.2, material='metal', ch='H', room='slag-yard')
box('anvil', -30.5, -28.5, -29.4, -27.4, 0, 0.9, material='metal', ch='A', room='slag-yard')
box('forge-hearth', -27.5, -24.6, -33.4, -31.0, 0, 1.4, material='brick', ch='f', pilot=True, room='slag-yard')
box('ingot-stack', -36.0, -34.0, -22.0, -19.5, 0, 1.2, material='metal', ch='o', room='slag-yard')

# =====================================================================================================================
# RECORDS: the exit door (anchor 0.6 m inside, interaction and closure on the inside, the approach, eight entries facing
# into the works), three caches (chest kind), the boss points in the converter hall.
ENTRIES_IN = [pt(x, 0, z, 270.0) for x in (47.0, 45.6) for z in (-2.6, -1.1, 1.1, 2.6)]
CACHES = [pt(38.6, 0, -6.4, 45.0), pt(6.5, L1, 24.2, 180.0), pt(-36.0, 0, -16.6, 180.0)]
BOSS_POINTS = [pt(-35.0, 0, 0.0, 90.0), pt(-34.0, 0, 7.5, 90.0), pt(-34.0, 0, -7.5, 90.0)]
# The direct converter shutter (owner decision 2026-10-16): a real instance gate filling the casting floor's west opening in
# wall-w (z -4..4, the ground openings' 5 m), sealed until the instance's boss encounter starts (`opens: "boss"`, PR #254's
# instance shutter, the existing one-second closure transition); until then the north and south loops are the way in.
GATE = dict(id=f'{AREA}:gate:converter', opens='boss', closure=vol(-24.0, 0, 0.0, 8.0, GO, WALL_T, 90.0))
def door_record():
    fx, fz = forward(YAW); s = -1.0                       # an instance exit: the party's side is behind the yaw (inside)
    cx, cz = DOOR
    return {'anchor': f'{AREA}:door:exit',
            'interact': vol(cx + s * fx * 1.1, 0, cz + s * fz * 1.1, 3.0, 3.0, 2.0, YAW),
            'closure': vol(cx + s * fx * 0.25, 0, cz + s * fz * 0.25, DOOR_W, DOOR_H, 0.4, YAW),
            'approach': pt(cx + s * fx * 1.6, 0, cz + s * fz * 1.6),
            'entries': ENTRIES_IN}

# =====================================================================================================================
# ENEMY SPAWNS (PveEntry shape plus `allow`, existing kinds only). Each record also gets a proposal in the ironworks record:
# its entrance style and the new Ironworks kinds it would admit (docs/EXPEDITION-CITY-IRONWORKS.md "Unique enemies").
SMALL = ['shambler', 'runner', 'spitter']; BIG = SMALL + ['brute']
entries, styles = [], {}
def entry(kind, name, frm, ext, facing, allow, style, propose, surface='', height=0.0, clear=1.2, hulk=None, level=0, extra=None):
    d = dict(e=0, id=f'{AREA}:spawn:{kind}-{name}', kind=kind, surface=surface, **{'from': v(*frm), 'exit': v(*ext)}, yaw=yaw_rad(*facing),
             level=level, song=-1, pit=False, height=r4(height), clear=r4(clear))
    if hulk is not None: d['hulk'] = hulk
    if extra: d.update(extra)
    d['allow'] = list(allow); entries.append(d); styles[d['id']] = (style, list(propose)); return d
def door(name, x, z, dx, dz, allow=SMALL, style='door', propose=('cinderling',), y=0.0, out=2.4):
    lvl = 1 if y > 3 else 0
    entry('door', name, (x - dx * 0.6, y, z - dz * 0.6), (x + dx * out, y, z + dz * out), (dx, dz), allow, style, propose, level=lvl)
def vent(name, x, z, dx, dz, style='vent', propose=('cinderling',), surface='floor', y=0.0, height=0.0):
    n = math.hypot(dx, dz); lvl = 1 if y > 3 else 0
    if surface == 'ceiling': entry('vent', name, (x, y, z), (x, y, z), (dx, dz), SMALL, style, propose, surface='ceiling', height=height, clear=1.6, level=lvl)
    else: entry('vent', name, (x, y, z), (x + dx / n * 1.2, y, z + dz / n * 1.2), (dx, dz), SMALL, style, propose, surface=surface, level=lvl)
def drop(name, x, z, height, dx, dz, hulk=False, style='gantry-drop', propose=('slagback',)):
    entry('drop', name, (x, 0, z), (x, 0, z), (dx, dz), ['hulk'] if hulk else ['brute'], style, propose, height=height, clear=4.5 if hulk else 2.8, hulk=hulk)
# The Rigger's hang points (owner decision 2026-10-16: the fifth kind). Each is a ceiling vent (an existing drop kind, so the
# strict parser and today's small kinds accept it) under a crane girder or the roof truss: the Rigger is lowered on its chain
# hook from the girder's underside, its feet RIG_HANG below it, then drops the last metres to the exit. `hangs` keeps, per spawn,
# the girder block it hangs from ('' for the roof truss, which is kit) and the hook's height, for the ironworks record.
RIG_HANG = 2.4                                  # the girder underside to the hanging Rigger's feet (hook 0.3, chain 0.2, body 1.9)
RIG_RING = 1.5                                  # the landing strike's radius (the telegraph ring and the hook's shadow)
TRUSS = 14.4                                    # the roof truss's bottom chord (kit) over the gantry points
hangs = {}
def rail(name, x, z, dx, dz, hang):
    b = next(b for b in blocks if b['id'] == f'{MAP}-{hang}'); bottom = yspan(b)[0]
    vent(name, x, z, dx, dz, style='crane-rail', propose=('rigger',), surface='ceiling', height=r4(bottom - RIG_HANG))
    hangs[f'{AREA}:spawn:vent-{name}'] = (b['id'], r4(bottom))
def gantry(name, x, z, dx, dz):
    vent(name, x, z, dx, dz, style='gantry', propose=('cinderling', 'stoker', 'rigger'), surface='ceiling', height=12.0)
    hangs[f'{AREA}:spawn:vent-{name}'] = ('', TRUSS)
BREACH_W, BREACH_H, BREACH_T = 3.2, 4.0, 0.4
def breach(name, face, axis, u, sign, room, style='breach', propose=('ladleman',)):
    f0, f1 = sorted((face, face + sign * BREACH_T)); a, b = u - BREACH_W / 2, u + BREACH_W / 2
    blk = box('breach-' + name, f0, f1, a, b, 0, BREACH_H, material='brick', ch='X', room=room) if axis == 'x' else box('breach-' + name, a, b, f0, f1, 0, BREACH_H, material='brick', ch='X', room=room)
    c, sz = blk['pos'], blk['size']; n = (sign, 0) if axis == 'x' else (0, sign)
    frm = (face - sign * 1.0, 0, u) if axis == 'x' else (u, 0, face - sign * 1.0)
    ext = (face + sign * (BREACH_T + 2.4), 0, u) if axis == 'x' else (u, 0, face + sign * (BREACH_T + 2.4))
    entry('breach', name, frm, ext, n, ['hulk'], style, propose, surface='wall', clear=HULK_CLEAR,
          extra=dict(block=blk['id'], center=v(c['x'], c['y'], c['z']), size=v(sz['x'], sz['y'], sz['z']), normal=v(n[0], 0, n[1])))
# Furnace mouths on the charging deck (level 1): the Cinderlings and Stokers walk out of the fire.
for k, x in enumerate((-15.0, 0.0, 15.0)): door(f'furnace-mouth-{k + 1}', x - 2.5, 28.0, 0, -1, y=L1, style='furnace-mouth', propose=('cinderling', 'stoker'))
# Tapholes and ash doors at the furnaces' feet (ground, under the deck).
for k, x in enumerate((-17.5, 2.5, 17.5)): door(f'ash-door-{k + 1}', x, 28.0, 0, -1, allow=BIG if k == 1 else SMALL, style='ash-door', propose=('cinderling', 'stoker'))
# Coke oven doors on the bench (ground).
for k, x in enumerate((-46.0, -40.5, -33.0, -27.5)): door(f'coke-oven-{k}', x, 28.0, 0, -1, allow=BIG if k == 2 else SMALL, style='coke-oven-door', propose=('cinderling', 'stoker'))
door('coke-walk', X0, 25.0, 1, 0, y=L1, style='coke-oven-door', propose=('stoker',), out=2.0)
# Casting floor: floor trenches (vents), the crane's load gantry (ceiling vents drop small kinds), the catwalk drops (brutes),
# the crane bay (a Hulk drop), a breach in the torpedo-car pit wall (the mill wall's east pier).
vent('trench-w', -6.0, -11.0, 0, 1, style='slag-trench', propose=('slagback', 'cinderling'))
vent('trench-e', 17.0, -10.5, 0, 1, style='slag-trench', propose=('slagback', 'cinderling'))
vent('trench-n', 6.5, 14.0, 0, -1, style='slag-trench', propose=('slagback', 'cinderling'))
gantry('gantry-0', -4.0, 9.0, 0, -1)
gantry('gantry-1', 22.0, 4.0, -1, 0)
# The Rigger's crane rails over the casting floor: under the runway girders' inside faces and the crane bridge.
rail('rail-n-w', -9.0, 20.0, 0, -1, 'crane-runway-n')
rail('rail-n-e', 24.0, 20.0, 0, -1, 'crane-runway-n')
rail('rail-s', 12.0, -10.2, 0, 1, 'crane-runway-s')
rail('bridge', 5.5, 1.5, 0, 1, 'crane-bridge')
drop('catwalk-w', -16.8, -8.0, L1, 1, 0, style='catwalk-drop', propose=('slagback',))
drop('catwalk-e', 24.5, -1.5, L1, -1, 0, style='catwalk-drop', propose=('slagback',))
drop('crane-bay', 3.0, 6.5, 12.0, 0, -1, hulk=True, style='crane-bay', propose=('ladleman',))
# North deck (level 1): a door from the flue gallery at the east end.
door('flue-gallery', 30.5, 28.0, 0, -1, y=L1, style='door', propose=('stoker',))
# Rolling mill: pits under the table (vents), doors in the south wall, a roof gantry, a Hulk breach in the south shell.
vent('mill-pit-0', 26.0, -30.5, 0, -1, style='mill-pit', propose=('cinderling', 'slagback'))
vent('mill-pit-1', 11.5, -31.0, 0, 1, style='mill-pit', propose=('cinderling', 'slagback'))
vent('mill-pit-2', -6.0, -18.5, 0, -1, style='mill-pit', propose=('cinderling', 'slagback'))
door('mill-store-s', -12.5, Z0, 0, 1, allow=BIG, style='door', propose=('stoker', 'slagback'))
door('mill-store-e', 32.5, Z0, 0, 1, style='door', propose=('cinderling',))
gantry('mill-roof', 3.5, -18.4, 1, 0)
breach('mill-s', Z0, 'z', 17.5, 1, 'rolling-mill')
# Stockhouse: bunker chutes onto the ore bridge (level 1, ceiling vents), a door at ground.
vent('chute-0', 39.0, 27.0, 0, -1, style='bunker-chute', propose=('cinderling',), surface='ceiling', y=L1, height=6.0)
vent('chute-1', 44.0, 27.0, 0, -1, style='bunker-chute', propose=('cinderling',), surface='ceiling', y=L1, height=6.0)
door('stock-yard', X1, 19.0, -1, 0, allow=BIG, style='door', propose=('cinderling', 'stoker'))
# Pattern shop: the store door (ground).
door('pattern-store', X1, -30.0, -1, 0, style='door', propose=('stoker',))
# Converter hall (the boss arena): converter mouths are boss business; the hall's spawns are a Hulk drop off the crane,
# a breach in the west shell, slag trenches either side of the pit.
drop('conv-crane', -29.5, 0.0, 11.0, -1, 0, hulk=True, style='crane-bay', propose=('ladleman',))
rail('conv-rail', -27.8, -6.0, -1, 0, 'conv-crane-runway-e')
breach('conv-w', X0, 'x', 0.0, 1, 'converter-hall')
vent('conv-trench-n', -40.0, 10.0, 1, 0, style='slag-trench', propose=('slagback',))
vent('conv-trench-s', -40.0, -10.0, 1, 0, style='slag-trench', propose=('slagback',))
# Slag yard: slag pits (vents) on the yard floor, the forge's quench tank door, a drop off the slag bank (brute).
vent('slag-pit-0', -36.5, -31.5, 1, 0, style='slag-pit', propose=('slagback',))
vent('slag-pit-1', -29.5, -15.5, 0, -1, style='slag-pit', propose=('slagback',))
vent('slag-pit-2', -41.0, -14.0, 1, 0, style='slag-pit', propose=('slagback',))
door('forge-store', -24.3, -19.5, -1, 0, style='door', propose=('cinderling', 'stoker'))
drop('slag-bank', -40.5, -24.0, 2.0, 1, 0, style='slag-bank', propose=('slagback',))
breach('forge-s', Z0, 'z', -30.0, 1, 'slag-yard')
entries.sort(key=lambda e: e['id'])
for i, e in enumerate(entries): e['e'] = i

# =====================================================================================================================
# THE IRONWORKS RECORD (additive; the strict loader ignores it, like the hub record). Rooms, the critical path, proposed
# hazards (ids start with the map id, as Astra's rule for map hazards), proposed machine spots, the boss arena, and per spawn
# its entrance style and the proposed Ironworks kinds. No balance here: the doc's Astra contract carries the numbers.
ROOMS = [  # (id, name, x0, x1, z0, z1, levels, role)
    ('time-office', 'Time Office', 36.3, 50.0, -9.7, 9.7, [0], 'entry: the way in and out; the first cache; a breath before the works'),
    ('stockhouse', 'Stockhouse and Ore Bridge', 36.3, 50.0, 10.3, 34.0, [0, 6], 'branch: up the stair to the ore bridge and the upper ring; bunker chutes overhead'),
    ('pattern-shop', 'Pattern Shop', 36.3, 50.0, -34.0, -10.3, [0], 'side room: the machine room (chip, med proposed); a quiet loop to the rolling mill'),
    ('casting-floor', 'Casting Floor', -23.7, 35.7, -13.7, 22.0, [0, 6], 'arena: the big fights under the ladle crane, ringed by the upper catwalks'),
    ('furnace-aisle', 'Furnace Aisle and Charging Deck', -23.7, 35.7, 22.0, 30.0, [0, 6], 'arena edge: three furnace mouths on the deck, tapholes and runners below'),
    ('rolling-mill', 'Rolling Mill', -23.7, 35.7, -34.0, -14.3, [0, 6], 'lane fight: a 60 m hall split by the roller table and four stands'),
    ('coke-ovens', 'Coke Oven Battery', -50.0, -24.3, 12.3, 34.0, [0, 6], 'choke: two lanes along the oven doors, the pusher car, the quench tower'),
    ('converter-hall', 'Converter Hall', -50.0, -24.3, -11.7, 11.7, [0], 'boss arena placeholder: three ways in, two converters, the pouring pit'),
    ('slag-yard', 'Slag Yard and Drop Forge', -50.0, -24.3, -34.0, -12.3, [0], 'risk and reward: the slag dump, the drop hammer, the third cache'),
]
PATH = ['time-office', 'casting-floor', ['furnace-aisle', 'coke-ovens'], ['rolling-mill', 'slag-yard'], 'converter-hall']
HAZARDS = [  # (id stem, kind, reuse, centre x, y, z, size x, y, z, cycle (warn, live, period s), room, note)
    ('runner-r1', 'molten-runner', 'new', -15.0, 0, 20.0, 1.2, 0.6, 16.0, (2.0, 6.0, 34.0), 'furnace-aisle', 'taphole opens, the trough fills white-orange from the furnace to the runner end; burns while live'),
    ('runner-r2', 'molten-runner', 'new', 0.0, 0, 20.0, 1.2, 0.6, 16.0, (2.0, 6.0, 34.0), 'furnace-aisle', 'as r1, offset 11 s'),
    ('runner-r3', 'molten-runner', 'new', 15.0, 0, 20.0, 1.2, 0.6, 16.0, (2.0, 6.0, 34.0), 'furnace-aisle', 'as r1, offset 22 s'),
    ('ladle-crane', 'crane', 'Factory crane (travel, warn, pour instead of drop)', 5.0, 0, -6.0, 6.0, 4.0, 5.0, (2.5, 3.0, 26.0), 'casting-floor', 'the crane carries the ladle along the bay and tips it over the mold rows; the splash zone burns'),
    ('drop-hammer', 'press', 'Factory press (vertical a->b)', -29.5, 0, -28.4, 2.4, 9.0, 2.4, (1.2, 0.35, 6.0), 'slag-yard', 'the ram falls on the anvil; crush'),
    ('slag-spill', 'slag-spill', 'new', -36.0, 0, -24.5, 6.0, 0.6, 4.0, (3.0, 10.0, 40.0), 'slag-yard', 'a pot tips; a glowing slag tongue spreads east over 3 s and cools over 10 s'),
    ('hot-slab', 'hot-slab', 'Factory conveyor (load on the roller table)', 4.5, 0.9, -25.0, 49.0, 1.0, 2.4, (1.5, 9.0, 18.0), 'rolling-mill', 'a red slab travels the table; touching it burns and carries'),
    ('flying-shear', 'saw-rail', 'Factory saw-rail / kit saw', -15.5, 1.3, -25.0, 0.4, 2.0, 2.6, (1.0, 0.6, 9.0), 'rolling-mill', 'the shear drops across the table when a slab passes'),
    ('quench-steam', 'vent', 'Factory vent (steam)', -44.0, 0, 18.0, 4.0, 6.0, 4.0, (1.5, 3.0, 22.0), 'coke-ovens', 'the quench car hisses: a steam column, push and scald'),
    ('pusher', 'pusher', 'new (a travelling press)', -37.0, 0, 26.0, 3.0, 3.6, 3.1, (3.0, 6.0, 30.0), 'coke-ovens', 'the pusher car runs the bench rail; crush in its path'),
    ('converter-tilt', 'converter', 'new (boss phase dressing)', -39.0, 0, 7.0, 4.0, 0.6, 4.0, (2.5, 4.0, 0.0), 'converter-hall', 'boss-driven only: the north converter tips and pours toward the pit (no cycle until a boss design exists)'),
]
# Owner decision 2026-10-16: keep the runners and the pour, Wave 1 runs light, the pour stops for the boss encounter. Per hazard:
# its state in Wave 1 ('on'; 'half': the period doubled; 'off') and during the boss encounter ('on'; 'off'; 'boss': the boss
# design drives it). Waves 2 to 4 run every proposed cycle as listed above.
SCHEDULE = {
    'runner-r1': ('off', 'on', 'Wave 1: the taphole stays plugged (a dull red glow); it taps from Wave 2'),
    'runner-r2': ('on', 'on', 'the one live runner in Wave 1, in the arena centre, where it teaches the read'),
    'runner-r3': ('off', 'on', 'as r1'),
    'ladle-crane': ('half', 'off', 'Wave 1: one pour every 52 s, warn 3.5 s; the boss encounter: the crane parks at the east end, ladle upright, no pour'),
    'drop-hammer': ('on', 'on', 'a fixed, readable press guarding cache 2'),
    'slag-spill': ('off', 'on', 'from Wave 3, when the slag yard joins'),
    'hot-slab': ('off', 'on', 'from Wave 2: the reheat door opens for the first time as the mill joins'),
    'flying-shear': ('off', 'on', 'runs with the hot slab'),
    'quench-steam': ('half', 'on', 'Wave 1: one blast every 44 s'),
    'pusher': ('off', 'on', 'from Wave 3, when the coke battery joins'),
    'converter-tilt': ('off', 'boss', 'boss-driven only'),
}
# Machine spots (owner decision 2026-10-16: the picks, with a second chip machine upstairs on the coke walkway). Spots only: a
# live machine needs PveContent authoring for the instance (Astra; NativeMachineRules.Present admits the map since PR #254).
MACHINES = [('chip', 46.6, 0, -16.0, 270.0, 'pattern-shop', 'the chip machine by the entry'), ('med', 46.6, 0, -24.0, 270.0, 'pattern-shop', 'the med machine beside it'),
            ('chip', -30.0, L1, 26.6, 180.0, 'coke-ovens', 'the second chip machine, upstairs on the coke walkway (the north loop)'), ('recruit', 39.0, 0, 7.0, 90.0, 'time-office', 'the recruit post in the time office')]
# Presentation tints for this zone only (owner decision 2026-10-16): the brute's orange seams vanish against the furnace glow, so
# in the Ironworks they draw cream. Existing kinds elsewhere keep NativeHordeKinds.Seam.
PRESENTATION = {'seams': [dict(kind='brute', table='NativeHordeKinds.Seam', default=0xFF6A2C, tint=0xFFD27A, scope=AREA)]}
ARENA = vol(-35.5, 0, 0.0, 22.0, 9.0, 22.0, 0)
def ironworks():
    rooms = [dict(id=f'{AREA}:room:{i}', name=n, volume=vol((a + b) / 2, 0, (c + d) / 2, b - a, 12.0, d - c, 0), levels=lv, role=role) for i, n, a, b, c, d, lv, role in ROOMS]
    hz = [dict(id=f'{MAP}-{i}', kind=k, reuse=reuse, volume=vol(x, y, z, sx, sy, sz, 0), warn=c[0], live=c[1], period=c[2], room=room, note=note, status='proposal',
               schedule=dict(wave1=SCHEDULE[i][0], boss=SCHEDULE[i][1], note=SCHEDULE[i][2]))
          for i, k, reuse, x, y, z, sx, sy, sz, c, room, note in HAZARDS]
    ms = []
    for k, (kind, x, y, z, yaw, room, note) in enumerate(MACHINES):
        fx, fz = forward(yaw); ms.append(dict(id=f'{AREA}:machine-proposal:{k}', kind=kind, x=r4(x), y=r4(y), z=r4(z), yaw=r4(yaw), front=pt(x + fx * 0.7, y, z + fz * 0.7), room=room,
                                             level=1 if y > 3 else 0, note=note, status='approved spot (owner 2026-10-16); not a live machine'))
    def hang(e):
        h = hangs.get(e['id']); return None if h is None else dict(block=h[0], hook=h[1], feet=r4(e['height']))
    sp = [dict(spawn=e['e'], style=styles[e['id']][0], propose=styles[e['id']][1], room=room_of(e['exit']), **({'hang': hang(e)} if e['id'] in hangs else {})) for e in entries]
    return {'status': 'design draft, not live; owner-approved design (2026-10-16)', 'doc': 'docs/EXPEDITION-CITY-IRONWORKS.md', 'rooms': rooms, 'path': PATH, 'hazards': hz, 'machines': ms,
            'arena': dict(room=f'{AREA}:room:converter-hall', volume=ARENA, boss='none yet (owner: bosses later)', shutter=GATE['id']), 'spawns': sp,
            'kinds': ['cinderling', 'stoker', 'slagback', 'ladleman', 'rigger'], 'presentation': PRESENTATION}
def room_of(p):
    for i, n, a, b, c, d, lv, role in ROOMS:
        if a - 1e-6 <= p['x'] <= b + 1e-6 and c - 1e-6 <= p['z'] <= d + 1e-6: return i
    return ''

# =====================================================================================================================
# THE MAP ENTRY (UnityMaps.json shape). The PvP spawn tables the map validator requires stand on the entry slots (y + 1.1);
# Expedition places pilots from the manifest's entries, never from these. nav is the importer's (tools/generate_expedition.py).
def clean(b): return {k: b[k] for k in ('id', 'material', 'pos', 'size', 'rotation', 'role', 'mass', 'segment', 'hp')}
def pilot_blocks():
    out = []
    for b in blocks:
        if not b['_pilot']: continue
        x0, x1, z0, z1 = footprint(b); top = yspan(b)[1]
        out.append(dict(id=b['id'] + '-top', center=v((x0 + x1) / 2, 0, (z0 + z1) / 2), size=v(x1 - x0, 0, z1 - z0), bottom=r4(top - 0.1)))
    return out
def game_map():
    sp = [dict(pos=v(s['x'], 1.1, s['z']), yaw=s['yaw']) for s in ENTRIES_IN]
    return dict(id=MAP, name='City Ironworks (Expedition)', tag='The sealed foundry. The furnaces never went out.', theme='expedition', music=None,
                ambience='', look=LOOK, sky=0x0C0908, floor=0x2A2522, accent=0xFF7A1F, rotation=False,
                bounds=dict(minX=X0, maxX=X1, minZ=Z0, maxZ=Z1, ceiling=CEIL, killY=KILL),
                spawns=dict(ffa=sp, teams=[dict(side=0, points=sp[:4]), dict(side=1, points=sp[4:])]),
                blocks=[clean(b) for b in blocks], segments=[], hazards=[], pilotBlocks=pilot_blocks(), nav=dict(nodes=[], links=[]),
                ramps=ramps, navHints=dict(drops=drops_hint, safe=[], collisionOff=[]))
def draft():
    return dict(schema=1, draft=True, note='DRAFT, NOT LIVE: the City Ironworks blockout (docs/arenas/scripts/expedition_ironworks.py, design docs/EXPEDITION-CITY-IRONWORKS.md); '
                'not game content and not in Resources/UnityMaps.json. Import needs the steps in that doc.', materials=[], maps=[game_map()])
def area():
    fx, fz = forward(YAW)
    anchors = [dict(id=f'{AREA}:door:exit', kind='door', building=AREA, **pt(DOOR[0] - fx * 0.6, 0, DOOR[1] - fz * 0.6, YAW))]
    anchors += [dict(id=f'{AREA}:cache:{i}', kind='chest', building='', **c) for i, c in enumerate(CACHES)]
    def front(p, d):
        fx_, fz_ = forward(p['yaw']); return dict(x=r4(p['x'] + fx_ * d), y=p['y'], z=r4(p['z'] + fz_ * d))
    return {'id': AREA, 'role': 'instance', 'map': MAP, 'blockHash': block_hash(game_map()),
            'anchors': anchors, 'doors': [door_record()],
            'interactions': [dict(anchor=f'{AREA}:cache:{i}', **front(c, 0.8), range=2.0) for i, c in enumerate(CACHES)],
            'gates': [GATE], 'spawns': entries, 'boss': {'points': BOSS_POINTS}, 'ironworks': ironworks()}

# =====================================================================================================================
# THE TOWN SIDE (the draft manifest only): the foundry door at the reserved doorway (-108, 0, 0 since the city grew in
# exp-layout-7; -84 before), yaw 90, built exactly as the
# town's Ward Zero door mirrored (expedition_city.door_record), its eight returns outside the shutter facing the town.
TOWN_DOOR = (-108.0, 0.0); TOWN_YAW = 90.0
TOWN_RETURNS = [pt(TOWN_DOOR[0] + dx, 0, z, TOWN_YAW) for dx in (4.4, 6.0) for z in (-3.0, -1.0, 1.0, 3.0)]
def live_manifest():
    return json.load(open(os.path.join(TESTS, 'expedition-layout-draft.json')))
def town_door():
    fx, fz = forward(TOWN_YAW); cx, cz = TOWN_DOOR
    anchor = dict(id=f'exp-city:door:{AREA}', kind='door', building=AREA, **pt(cx + fx * 0.6, 0, cz + fz * 0.6, TOWN_YAW))
    rec = {'anchor': anchor['id'], 'interact': vol(cx + fx * 1.0, 0, cz + fz * 1.0, 3.0, 3.0, 2.0, TOWN_YAW),
           'closure': vol(cx + fx * 0.2, 0, cz + fz * 0.2, DOOR_W, DOOR_H, 0.4, TOWN_YAW), 'approach': pt(cx + fx * 1.6, 0, cz + fz * 1.6), 'returns': TOWN_RETURNS}
    return anchor, rec
def manifest():
    m = live_manifest(); m['revision'] = REVISION; m['instances'] = list(m['instances']) + [AREA]
    town = next(a for a in m['areas'] if a['id'] == m['world']); anchor, rec = town_door()
    k = max(i for i, a in enumerate(town['anchors']) if a['kind'] == 'door') + 1
    town['anchors'] = town['anchors'][:k] + [anchor] + town['anchors'][k:]; town['doors'] = list(town['doors']) + [rec]
    m['areas'] = list(m['areas']) + [area()]
    return m

# =====================================================================================================================
# THE LOOK (MapLooks.json shape, schema 1; not in Resources/MapLooks.json). A sealed interior (no sky, no shadowing key: the
# roof would shadow it, as in Ward Zero's quarantine look): a cold blue-grey fill from the roof monitors keeps the dark iron and
# the enemies' silhouettes in shape, a low warm rim from the furnace line (north), a warm ground bounce in the ambient; the
# molten metal and the furnaces are emissive, sodium lamps the warm work light, mercury-vapour lamps the cold one. The eight
# practical lights (MaxLights) are the casting floor's default set (six warm, two cool); the doc's per-room table is the
# candidate list for the proposed picker, which keeps two slots for the cool lamps.
def look():
    L = lambda id, x, y, z, color, intensity, rng, flicker='', rate=0, period=0, length=0, depth=0, phase=0: dict(
        id=id, flicker=flicker, pos=v(x, y, z), color=color, intensity=intensity, range=rng, rate=rate, period=period, length=length, depth=depth, phase=phase)
    BL = lambda block, look, color, note, band=False: dict(block=f'{MAP}-{block}', look=look, color=color, band=band, note=note)
    return dict(id=MAP, look=LOOK, farClip=240, background=0x0B0807,
                key=dict(color=0x8A9CC0, intensity=0.22, elevation=72, bearing=210, **{'from': v(0, 0, 0)}, shadows='none', shadowStrength=1),
                rim=dict(color=0x6A7FA8, intensity=0.16, elevation=38, bearing=200, **{'from': v(0, 0, 0)}, shadows='none', shadowStrength=1),
                ambient=dict(sky=0x5A6A80, skyScale=0.09, equator=0x4A443E, equatorScale=0.07, ground=0x3A2A1E, groundScale=0.08),
                fog=dict(mode='linear', color=0x221A16, skyLerp=0, start=14, end=120, density=0),
                shadows=dict(cascades=2, distance=60, fallbackCascades=2, fallbackDistance=45),
                sky=dict(variant='none', horizon=0x1A0F0A, zenith=0x0B0807, glow=0, disc=0, cloud=0, discSize=0.028, halo=0.083333336, stars=0, crater=0.24, cloudSpeed=1,
                         elevation=0, bearing=0, dir=v(-0.4, 0.46, -0.78)),
                lights=[L('furnace-2-doors', 0.0, 7.4, 26.2, 0xFF6A1C, 1.6, 12, 'fire', 1.1, depth=0.22),
                        L('ladle-pour', 7.3, 2.6, -7.0, 0xFFB45A, 2.4, 13, 'fire', 3.1, depth=0.3, phase=1.1),
                        L('runner-r2', 0.0, 0.8, 16.5, 0xFF7A26, 1.2, 8, 'fire', 2.2, depth=0.18, phase=0.4),
                        L('reheat-door', 28.2, 1.8, -25.0, 0xFF5A18, 1.9, 13, 'fire', 0.9, depth=0.2),
                        L('sodium-mill', 6.0, 10.4, -25.0, 0xFFB347, 0.7, 15),
                        L('sodium-cast', 18.0, 12.0, 4.0, 0xFFB347, 0.4, 14, 'stutter', period=11, length=0.25, depth=0.5),
                        L('mercury-cast-w', -14.0, 11.7, 12.0, 0xBFD8FF, 0.5, 16),
                        L('mercury-cast-s', 0.0, 11.7, -11.0, 0xBFD8FF, 0.45, 15)],
                blockLooks=[BL('slab', 'concrete-board', 0x5A544C, 'the casting floor slab: sooted concrete, light enough to read under the bay lights'),
                            BL('roof', 'corrugated', 0x2A2826, 'roof sheeting (skylight monitors are kit)'),
                            BL('shell-*', 'corrugated', 0x2E2C2A, 'the shell cladding'),
                            BL('exit-*', 'steel-green', 0x4A5A4E, 'the exit door frame'),
                            BL('furnace-*', 'brick-buff', 0x6A5040, 'refractory brick (the mouths and tapholes are kit, lit)'),
                            BL('flue-wall*', 'brick-red', 0x3A2620, 'the flue wall behind the furnaces'),
                            BL('battery*', 'brick-red', 0x3A2018, 'the coke oven battery (oven doors are kit)'),
                            BL('quench-tower*', 'brick-red', 0x46302A, 'the quench tower'),
                            BL('reheat-furnace*', 'brick-buff', 0x5E4636, 'the reheat furnace (its doors glow; kit)'),
                            BL('forge-hearth*', 'brick-buff', 0x6A4A38, 'the forge hearth'),
                            BL('runner-*', 'brick-buff', 0x6A4E3E, 'runner curbs (the metal in the trough is kit)'),
                            BL('pit-rim-*', 'brick-buff', 0x6A4E3E, 'the converter pit rim'),
                            BL('wall-*', 'brick-red', 0x40281E, 'partitions: soot-black brick'),
                            BL('breach-*', 'brick-red', 0x6A4434, 'Hulk breach panels: bricked-up openings'),
                            BL('deck-*-rail-*', 'factory-grating', 0xC9A23A, 'guard rails: safety yellow mesh panels (the kit adds the handrail tube and posts)'),
                            BL('deck-*-col-*', 'steel-green', 0x3C3E3A, 'deck columns'),
                            BL('deck-*', 'factory-grating', 0x4A4C4A, 'the upper ring and walkways: open grating'),
                            BL('stair-*', 'factory-grating', 0x4A4C4A, 'stairs'),
                            BL('torpedo-*', 'cyber-metal', 0x3A3632, 'torpedo ladle cars (black iron plate, refractory lips are kit)'),
                            BL('ladle-stand-*', 'cyber-metal', 0x403A34, 'ladles on their stands'),
                            BL('crane-*', 'cyber-metal', 0xB8902C, 'the crane: safety yellow plate'),
                            BL('conv-crane-*', 'cyber-metal', 0xB8902C, 'the converter hall crane runways: safety yellow plate'),
                            BL('converter-*', 'cyber-metal', 0x3A3430, 'the converters: black iron plate (vessel bands and mouths are kit)'),
                            BL('trunnion-*', 'steel-green', 0x56504A, 'converter trunnions'),
                            BL('mill-stand-*', 'cyber-metal', 0x4A5A50, 'mill stand housings: machine-green plate (edges, windows, chocks and gears are kit)'),
                            BL('shear-housing-*', 'cyber-metal', 0x4A5A50, 'the flying shear housing: machine-green plate'),
                            BL('roller-table-*', 'steel-green', 0x34302C, 'roller tables (the rollers are kit)'),
                            BL('cooling-bed', 'factory-grating', 0x3A3A38, 'the cooling bed'),
                            BL('pulpit', 'corrugated', 0x5A6060, 'the mill pulpit (lit windows are kit)'),
                            BL('reheat*', 'brick-buff', 0x7A5A44, 'reheat furnace'),
                            BL('slag-bank', 'asphalt', 0x1E1A18, 'the slag bank: black glassy slag'),
                            BL('slag-pot-*', 'cyber-metal', 0x3A3430, 'slag pot cars'),
                            BL('hammer-*', 'steel-green', 0x3E4A44, 'the drop hammer frame'),
                            BL('anvil', 'steel-green', 0x2E2C2A, 'the anvil'),
                            BL('bunkers*', 'corrugated', 0x4A4440, 'ore bunkers'),
                            BL('skip-housing*', 'corrugated', 0x4A4440, 'the skip incline housing'),
                            BL('aisle-bay-*', 'brick-red', 0x40281E, 'aisle bay wall'),
                            BL('pattern-rack-*', 'timber', 0, 'pattern racks (wooden patterns)'),
                            BL('mold-boxes', 'timber', 0, 'mold boxes'),
                            BL('bench', 'timber', 0, 'the time office bench'),
                            BL('lockers-*', 'steel-green', 0x56605A, 'lockers'),
                            BL('clock-rack', 'steel-green', 0x56605A, 'the clock card rack (the shift board is kit)'),
                            BL('tap-cart-*', 'steel-green', 0x4A443C, 'taphole tool carts'),
                            BL('scrap-box-*', 'corrugated', 0x5A4A3A, 'scrap boxes'),
                            BL('charge-box-*', 'corrugated', 0x5A4A3A, 'charging boxes on the deck'),
                            BL('mold-row-*', 'steel-green', 0x2E2A26, 'ingot molds (cast iron)'),
                            BL('pig-bed', 'steel-green', 0x34302C, 'the pig casting bed'),
                            BL('skull-pile', 'asphalt', 0x24201C, 'ladle skulls: solidified iron'),
                            BL('mill-drive', 'cyber-metal', 0x4A5A50, 'stand drive motors: machine-green plate'),
                            BL('coil-stack-*', 'steel-green', 0x6A6460, 'coils'),
                            BL('billet-rack', 'steel-green', 0x5A4A40, 'billets on a rack'),
                            BL('conveyor-frame', 'factory-grating', 0x4A4C4A, 'the ore conveyor'),
                            BL('ore-cart', 'steel-green', 0x5A3C2A, 'an ore cart (rust)'),
                            BL('sand-muller', 'steel-green', 0x4E5A50, 'the sand muller'),
                            BL('pusher-car', 'cyber-metal', 0xB8902C, 'the coke pusher car: safety yellow plate'),
                            BL('hopper-*', 'corrugated', 0x3A3632, 'coal hoppers'),
                            BL('ingot-stack', 'steel-green', 0x4A4440, 'an ingot stack'),
                            BL('material:metal', 'steel-green', 0x3E3A36, 'other ironwork'),
                            BL('material:brick', 'brick-red', 0x40281E, 'other brick'),
                            BL('material:wood', 'timber', 0, 'other timber'),
                            BL('material:stone', 'concrete-board', 0x4A4440, 'other masonry'),
                            BL('material:foundation', 'concrete-board', 0x3A3430, 'floor')],
                kitLooks=[], dressing='ironworks', backdrop='', atmosphere='ironworks', ambience='ironworks',
                blocks=dict(dressed=True, dust=0.12, haze=0.25, hazeTop=4.0, hazeColor=0x1E1612))
def look_set(): return dict(schema=1, maps=[look()], preview=[])

# =====================================================================================================================
# CHECKS
def solid(b): return yspan(b)[1] - yspan(b)[0] > 0.02
def overlaps():
    """Pairs of blocks whose interiors overlap by more than 1 cm on every axis (authored boxes are axis-aligned here)."""
    bad = []; bs = [b for b in blocks if b['id'] not in (MAP + '-slab', MAP + '-roof')]
    bucket = {}
    for i, b in enumerate(bs):
        x0, x1, z0, z1 = footprint(b)
        for bi in range(int(math.floor(x0 / 4)), int(math.floor(x1 / 4)) + 1):
            for bj in range(int(math.floor(z0 / 4)), int(math.floor(z1 / 4)) + 1): bucket.setdefault((bi, bj), []).append(i)
    seen = set()
    for idx in bucket.values():
        for a in idx:
            for c in idx:
                if c <= a or (a, c) in seen: continue
                seen.add((a, c)); A, C = bs[a], bs[c]
                ax0, ax1, az0, az1 = footprint(A); cx0, cx1, cz0, cz1 = footprint(C); ay0, ay1 = yspan(A); cy0, cy1 = yspan(C)
                if min(ax1, cx1) - max(ax0, cx0) > 0.01 and min(az1, cz1) - max(az0, cz0) > 0.01 and min(ay1, cy1) - max(ay0, cy0) > 0.01: bad.append((A['id'], C['id']))
    return bad
def checks():
    out, ok = [], True
    m = game_map(); col = Collider(m['blocks'], m['ramps'], m['bounds'])
    keep = lambda y: -0.01 <= y <= CEIL - 1
    col.build(keep)
    starts = []
    for i, s in enumerate(ENTRIES_IN):
        n = col.node_at(s['x'], s['y'], s['z']); assert n, f'entry slot {i} not on walkable floor'; starts.append(n)
    reach = col.flood(starts)
    def floor(n, y): return abs(n[2] - y) < 0.05
    for lvl, y in (('ground', 0.0), ('deck', L1)):
        tot = [n for n in col.graph if floor(n, y)]; got = [n for n in tot if n in reach]
        out.append(f'{lvl} floor (y {y:g}): {len(tot)} standable 0.5 m cells ({len(tot) * 0.25:.0f} m2), {len(got)} reached from the entry slots ({100 * len(got) / max(1, len(tot)):.1f}%)')
        if len(got) != len(tot):
            ok = False
            for g in components(col, [n for n in tot if n not in reach])[:8]: out.append('   UNREACHED %d cells round (%.1f, %.1f)' % g)
    other = [n for n in col.graph if not floor(n, 0.0) and not floor(n, L1)]
    out.append(f'other standable tops (stairs, cover, rails): {len(other)} cells, {len([n for n in other if n in reach])} reached')
    ids = [b['id'] for b in m['blocks']]
    out.append(f'blocks: {len(ids)} ({len(ramps)} stairs, {len(m["pilotBlocks"])} pilot exclusion columns), ids unique: {len(set(ids)) == len(ids)}, all {MAP}-*: {all(i.startswith(MAP + "-") for i in ids)}')
    ok &= len(set(ids)) == len(ids) and all(i.startswith(MAP + '-') for i in ids) and 200 <= len(ids) <= 700
    ov = overlaps(); out.append(f'solid blocks overlapping another by more than 1 cm on every axis: {len(ov)}' + ''.join(f'\n   BAD {a} x {b}' for a, b in ov[:12])); ok &= not ov
    inb = all(x0 >= X0 - 1 - 1e-6 and x1 <= X1 + 1 + 1e-6 and z0 >= Z0 - 1 - 1e-6 and z1 <= Z1 + 1 + 1e-6 for x0, x1, z0, z1 in map(footprint, blocks)) and all(yspan(b)[1] <= CEIL + 0.6 + 1e-6 for b in blocks)
    out.append(f'every block inside the shell (1 m round the bounds) and under the roof top: {inb}'); ok &= inb
    # Pilot exclusion columns never stand under a deck (they would push pilots off the walkway above).
    under = [p['id'] for p in m['pilotBlocks'] for d in decks if min(p['center']['x'] + p['size']['x'] / 2, footprint(d)[1]) - max(p['center']['x'] - p['size']['x'] / 2, footprint(d)[0]) > 0
             and min(p['center']['z'] + p['size']['z'] / 2, footprint(d)[3]) - max(p['center']['z'] - p['size']['z'] / 2, footprint(d)[2]) > 0 and p['bottom'] < L1 + 2]
    out.append(f'pilot exclusion columns under a deck: {len(under)}' + ''.join('\n   BAD ' + u for u in under)); ok &= not under
    sl = all(r['high']['y'] - r['low']['y'] < math.hypot(r['high']['x'] - r['low']['x'], r['high']['z'] - r['low']['z']) and r['width'] > 0.84 for r in ramps)
    out.append(f'stairs: {len(ramps)}, slope 0.5 (under the ramp rule of 1), widths {sorted({r["width"] for r in ramps})}: {sl}'); ok &= sl
    # Each stair top lands on a deck cell and each foot on the ground, both reached.
    for r in ramps:
        lo, hi = r['low'], r['high']; L = math.hypot(hi['x'] - lo['x'], hi['z'] - lo['z']); ux, uz = (hi['x'] - lo['x']) / L, (hi['z'] - lo['z']) / L
        a = col.node_at(lo['x'] - ux * 0.75, 0, lo['z'] - uz * 0.75); b = col.node_at(hi['x'] + ux * 0.75, L1, hi['z'] + uz * 0.75)
        good = a in reach and b in reach
        if not good: out.append(f'   BAD stair {r["id"]}: foot {a in reach}, top {b in reach}')
        ok &= good
    d = door_record(); a = area()
    def check_points(label, pts, need, yhi=HEIGHT + 0.05):
        bad = []; worst = 99
        for name, p in pts:
            n = col.node_at(p['x'], p['y'], p['z']); f = n is not None and n in reach and col.floor_under(p['x'], p['z'], p['y'])
            c = col.clearance(p['x'], p['z'], p['y'] + 0.35, p['y'] + yhi); worst = min(worst, c)
            if not f or c < need: bad.append(f"{name} ({p['x']:g}, {p['y']:g}, {p['z']:g}) floor {f} clear {c:.2f}")
        out.append(f'{label}: {len(pts)} on reached floor at their own level with {need:g} m clear: {len(pts) - len(bad)} (tightest {worst:.2f} m)' + ''.join('\n   BAD ' + s for s in bad))
        return not bad
    ok &= check_points('entry slots', [(f'entry{i}', p) for i, p in enumerate(ENTRIES_IN)], PILOT_CLEAR)
    ok &= check_points('door approach', [('approach', d['approach'])], PILOT_CLEAR)
    ok &= check_points('cache anchors (piece footprint)', [(c['id'], c) for c in a['anchors'] if c['kind'] == 'chest'], 1.0)
    ok &= check_points('interaction points', [(c['anchor'], c) for c in a['interactions']], PILOT_CLEAR)
    ok &= check_points('boss points (Hulk-sized)', [(f'boss{i}', p) for i, p in enumerate(BOSS_POINTS)], HULK_CLEAR, yhi=HULK_BAND)
    ok &= check_points('proposed machine spots (piece footprint)', [(x['id'], x) for x in a['ironworks']['machines']], 0.9)
    ok_d = in_vol(d['interact'], d['approach']['x'], 0.0, d['approach']['z']) and not any(in_vol(d['interact'], p['x'], 0.0, p['z']) for p in ENTRIES_IN)
    ok_d &= col.floor_under(d['interact']['x'], d['interact']['z'], 0.0) and col.floor_under(d['closure']['x'], d['closure']['z'], 0.0)
    jam = [b for b in blocks if 'exit-jamb' in b['id']]; cl = vol_block('', d['closure'])
    gap = min(dist_xz(b, x, z) for b in jam for x, z in corners(cl)); ok_d &= gap < 0.05 and abs(d['closure']['sx'] - (dz1 - dz0)) < 1e-6
    out.append(f'exit door: doorway ({DOOR[0]:g}, 0, {DOOR[1]:g}) yaw {YAW:g} in the east shell, anchor 0.6 m inside, closure {d["closure"]["sx"]:g} x {d["closure"]["sy"]:g} m between the jambs, approach inside the interaction volume, entries outside it: {ok_d}'); ok &= ok_d
    sl = min(math.hypot(a_['x'] - b_['x'], a_['z'] - b_['z']) for i, a_ in enumerate(ENTRIES_IN) for b_ in ENTRIES_IN[i + 1:])
    out.append(f'entry slots: closest pair {sl:.2f} m'); ok &= sl >= 1.4 - 1e-6
    # The loader's gate mirrored: entries Hulk-clear; anchors, interactions and the approach pilot-clear; pilot and Hulk routes
    # from entry 0 to every entry, boss point and the approach; every spawn exit routed back with its capsule.
    hulk = Collider(m['blocks'], m['ramps'], m['bounds']); hulk.build(keep, HULK_RADIUS, HULK_HEIGHT)
    hs = min((col.clearance(p['x'], p['z'], p['y'] + 0.35, p['y'] + HULK_HEIGHT + 0.05), f'entry{i}') for i, p in enumerate(ENTRIES_IN))
    out.append('entry slots clear for a Hulk (%.3f m radius, %.2f m tall): tightest %.2f m (%s)' % (HULK_SLOT, HULK_HEIGHT, hs[0], hs[1])); ok &= hs[0] >= HULK_SLOT
    pr = min((col.clearance(p['x'], p['z'], p['y'] + 0.35, p['y'] + HEIGHT + 0.05), n) for n, p in [(x['id'], x) for x in a['anchors']] + [(x['anchor'], x) for x in a['interactions']] + [('approach', d['approach'])])
    out.append('anchors (the door too), interactions and the approach clear for the pilot probe (%.2f m, %.1f m): tightest %.2f m (%s)' % (RADIUS, HEIGHT, pr[0], pr[1])); ok &= pr[0] >= RADIUS
    o = ENTRIES_IN[0]; rp = col.flood([col.node_at(o['x'], o['y'], o['z'])]); rh = hulk.flood([hulk.node_at(o['x'], o['y'], o['z'])])
    need = ENTRIES_IN + BOSS_POINTS + [d['approach']]
    miss = [p for p in need if hulk.node_at(p['x'], p['y'], p['z']) not in rh or col.node_at(p['x'], p['y'], p['z']) not in rp]
    out.append(f'Hulk-sized and pilot routes from entry 0 to every entry, boss point and the approach: {len(need) - len(miss)} of {len(need)}'); ok &= not miss
    hg = [n for n in hulk.graph if abs(n[2]) < 0.05]; hr = [n for n in hg if n in rh]
    out.append(f'Hulk-standable ground reached by a Hulk from entry 0: {100 * len(hr) / max(1, len(hg)):.1f}% of {len(hg)} cells')
    # Spawns.
    kinds = {}
    for e in entries: kinds[e['kind'] + ('-ceiling' if e.get('surface') == 'ceiling' else '') + ('-hulk' if e.get('hulk') else '')] = kinds.get(e['kind'] + ('-ceiling' if e.get('surface') == 'ceiling' else '') + ('-hulk' if e.get('hulk') else ''), 0) + 1
    out.append(f'spawns: {len(entries)} (e 0..{len(entries) - 1}): ' + ', '.join(f'{k} {n}' for k, n in sorted(kinds.items())) + f'; level 1 (deck): {sum(1 for e in entries if e["level"] == 1)}')
    ok &= [e['e'] for e in entries] == list(range(len(entries))) and 36 <= len(entries) <= 64
    def allows(e, k): return (e['kind'] == 'breach' or e['kind'] == 'drop' and e.get('hulk')) if k == 'hulk' else e['kind'] != 'breach' and (e['kind'] != 'drop' or k == 'brute' and not e.get('hulk'))
    al = all(e['allow'] and all(allows(e, k) for k in e['allow']) for e in entries)
    out.append(f'allow lists within the entry rules (Hulk: breach or Hulk drop; drops: brute; doors and vents: shambler, runner, spitter, brute): {al}'); ok &= al
    bad = []
    for e in entries:
        x = e['exit']; heavy = 'hulk' in e['allow']; drop_ = e['kind'] == 'drop' or e.get('surface') == 'ceiling'
        g = (hulk if heavy else col); n = g.node_at(x['x'], x['y'], x['z'], 0.36 if heavy else 0.6)
        if n is None or n not in (rh if heavy else rp): bad.append(f"{e['id']} exit not routed to entry 0 with its capsule"); continue
        need_c = e['clear']; yhi = (e['height'] + 0.5 if drop_ else (HULK_BAND if heavy else HEIGHT + 0.05))
        c = col.clearance(x['x'], x['z'], x['y'] + 0.35, x['y'] + yhi)
        if c < need_c - 1e-6: bad.append(f"{e['id']} clear {c:.2f} < {need_c:g} m (up to {yhi:.1f} m)")
        if e['kind'] == 'breach':
            pan = next(b for b in blocks if b['id'] == e['block']); ok_p = pan['pos'] == e['center'] and pan['size'] == e['size']
            if not ok_p: bad.append(e['id'] + ' panel')
    out.append(f"spawn exits on reached floor at their own level with their clear radius and routed to entry 0 with their own capsule (Hulk where allowed): {len(entries) - len(bad)} of {len(entries)}" + ''.join('\n   BAD ' + s for s in bad))
    ok &= not bad
    ex = [(e['exit']['x'], e['exit']['y'], e['exit']['z'], e['id']) for e in entries]
    sp = min((math.hypot(a_[0] - b_[0], a_[2] - b_[2]), a_[3], b_[3]) for i, a_ in enumerate(ex) for b_ in ex[i + 1:] if abs(a_[1] - b_[1]) < 1)
    out.append('spawn exits: closest pair on one level %.1f m (%s, %s)' % sp); ok &= sp[0] >= 3.0
    dist = {n: 0 for n in starts}; frontier = list(starts)
    while frontier:
        nxt = []
        for n in frontier:
            for k in col.neighbours(n):
                if k not in dist: dist[k] = dist[n] + 1; nxt.append(k)
        frontier = nxt
    wk = min((dist.get(col.node_at(e['exit']['x'], e['exit']['y'], e['exit']['z']), 10 ** 9) * 0.5, e['id']) for e in entries)
    out.append('nearest spawn exit to the entry slots on foot: %.1f m (%s)' % wk); ok &= wk[0] >= 12.0
    recs = [(f'entry{i}', p) for i, p in enumerate(ENTRIES_IN)] + [('approach', d['approach'])] + [(c['id'], c) for c in a['anchors']]
    md = min((math.hypot(e['exit']['x'] - p['x'], e['exit']['z'] - p['z']), e['id'], n) for e in entries for n, p in recs if abs(e['exit']['y'] - p['y']) < 1)
    out.append('nearest spawn exit to an entry slot, the door or a cache in a straight line: %.1f m (%s, %s)' % md); ok &= md[0] >= 4.0
    bp = [(e['id'], i) for e in entries for i, p in enumerate(BOSS_POINTS) if math.hypot(e['exit']['x'] - p['x'], e['exit']['z'] - p['z']) < 4 and abs(e['exit']['y'] - p['y']) < 1]
    out.append('spawn exits within 4 m of a boss point: ' + (', '.join(f'{i} (boss{k})' for i, k in bp) or 'none')); ok &= not bp
    # The critical path on foot (pilot, 0.5 m grid, 8-connected): entry 0 to the boss arena directly (the casting floor's west
    # opening), by the north loop (furnace aisle, coke ovens) and by the south loop (rolling mill, slag yard), the openings of
    # the other routes forbidden in turn.
    bossn = col.node_at(BOSS_POINTS[0]['x'], 0, BOSS_POINTS[0]['z'])
    def cell(n): return col.cell_xz(n[0], n[1])
    west_gap = lambda n: abs(cell(n)[0] + 24.0) < 0.6 and abs(cell(n)[1]) < 4.5
    routes = {'direct': None, 'north loop': lambda n: west_gap(n) or cell(n)[1] < -12.5, 'south loop': lambda n: west_gap(n) or cell(n)[1] > 12.5}
    LOOPS.clear()
    for name, forbid in routes.items():
        dd = walk(col, starts[0], forbid); LOOPS[name] = dd.get(bossn)
    out.append('entry slot 0 to the converter hall (boss point 0) on foot: ' + ', '.join(f'{k} {v_:.1f} m ({v_ / 6.8:.1f} s running)' if v_ else f'{k} UNREACHED' for k, v_ in LOOPS.items()))
    ok &= all(LOOPS.values())
    # Hazard proposals clear of every record (2 m), as Ward Zero's records keep 2 m from Siren's hazard volumes.
    hz = []
    for h in a['ironworks']['hazards']:
        V = h['volume']
        for n, p in recs + [(f'boss{i}', p) for i, p in enumerate(BOSS_POINTS)] + [(e['id'], e['exit']) for e in entries]:
            if V['y'] - V['sy'] / 2 < p['y'] + 2 and V['y'] + V['sy'] / 2 > p['y'] - 0.01 and math.hypot(max(abs(p['x'] - V['x']) - V['sx'] / 2, 0), max(abs(p['z'] - V['z']) - V['sz'] / 2, 0)) < (0.5 if 'spawn' in n else 2.0):
                hz.append((n, h['id']))
    out.append(f'records within 2 m (spawn exits 0.5 m) of a proposed hazard volume: {len(hz)}' + ''.join(f'\n   BAD {n} in {h}' for n, h in hz)); ok &= not hz
    # The hazard schedule (owner decision): Wave 1 light (at most one runner live, the pour at half rate or slower), no pour in
    # the boss encounter, every hazard scheduled.
    sch = {h['id'][len(MAP) + 1:]: h['schedule'] for h in a['ironworks']['hazards']}
    sok = set(sch) == set(SCHEDULE) and all(x['wave1'] in ('on', 'half', 'off') and x['boss'] in ('on', 'off', 'boss') for x in sch.values())
    sok &= sum(1 for k, x in sch.items() if k.startswith('runner-') and x['wave1'] == 'on') == 1 and sch['ladle-crane']['wave1'] in ('half', 'off') and sch['ladle-crane']['boss'] == 'off'
    w1 = {st: sorted(k for k, x in sch.items() if x['wave1'] == st) for st in ('off', 'half')}
    out.append(f"hazard schedule: Wave 1 off {', '.join(w1['off'])}; at half rate {', '.join(w1['half'])}; one runner live; the ladle pour off in the boss encounter: {sok}"); ok &= sok
    # The converter shutter: an instance gate (opens "boss") filling the west opening, in the bounds; shut, the converter hall and
    # every boss point are still reached (pilot and Hulk) by the loops only, and every spawn exit still routes to entry 0.
    G = GATE['closure']; gb = dict(vol_block(MAP + '-gate-converter', G), role='static', material='metal')
    fills = all(in_vol(G, -24.0, y, z) for y in (0.2, 2.5, GO - 0.05) for z in (-3.95, -2.0, 0.0, 2.0, 3.95)) and abs(vol_floor(G)) < 1e-6
    piers = [b for b in blocks if b['id'].startswith(MAP + '-wall-w') and footprint(b)[0] < -23.9 < footprint(b)[1] and yspan(b)[0] < 0.1 and (footprint(b)[3] <= -3.99 or footprint(b)[2] >= 3.99)]
    seal = fills and min(dist_xz(b, -24.0, -4.0) for b in piers) < 0.01 and min(dist_xz(b, -24.0, 4.0) for b in piers) < 0.01
    lintel = [b for b in blocks if b['id'].startswith(MAP + '-wall-w') and dist_xz(b, -24.0, 0.0) < 1e-6 and abs(yspan(b)[0] - GO) < 1e-6]
    seal &= bool(lintel) and vol_in_bounds(G, m['bounds']) and GATE['opens'] == 'boss' and not [b['id'] for b in blocks if solid(b) and b['id'] not in (MAP + '-slab',) and
                 min(footprint(gb)[1], footprint(b)[1]) - max(footprint(gb)[0], footprint(b)[0]) > 0.01 and min(footprint(gb)[3], footprint(b)[3]) - max(footprint(gb)[2], footprint(b)[2]) > 0.01
                 and min(yspan(gb)[1], yspan(b)[1]) - max(yspan(gb)[0], yspan(b)[0]) > 0.01]
    out.append(f"converter shutter {GATE['id']} (opens {GATE['opens']}): {G['sx']:g} x {G['sy']:g} x {G['sz']:g} m in wall-w's west opening, piers either side and the lintel over it, inside the bounds, overlapping no block: {seal}"); ok &= seal
    gp = Collider(m['blocks'] + [gb], m['ramps'], m['bounds']); gp.build(keep); gh = Collider(m['blocks'] + [gb], m['ramps'], m['bounds']); gh.build(keep, HULK_RADIUS, HULK_HEIGHT)
    g0 = gp.node_at(o['x'], o['y'], o['z']); gpr = gp.flood([g0]); ghr = gh.flood([gh.node_at(o['x'], o['y'], o['z'])])
    gmiss = [p for p in BOSS_POINTS if gp.node_at(p['x'], p['y'], p['z']) not in gpr or gh.node_at(p['x'], p['y'], p['z']) not in ghr]
    gbad = []
    for e in entries:
        x = e['exit']; heavy = 'hulk' in e['allow']; g = gh if heavy else gp; n = g.node_at(x['x'], x['y'], x['z'], 0.36 if heavy else 0.6)
        if n is None or n not in (ghr if heavy else gpr): gbad.append(e['id'])
    shut = walk(gp, g0).get(gp.node_at(BOSS_POINTS[0]['x'], 0, BOSS_POINTS[0]['z'])); direct = walk(gp, g0, lambda n: abs(gp.cell_xz(n[0], n[1])[1]) > 12.5).get(gp.node_at(BOSS_POINTS[0]['x'], 0, BOSS_POINTS[0]['z']))
    gok = not gmiss and not gbad and shut is not None and direct is None and shut >= min(LOOPS['north loop'], LOOPS['south loop']) - 0.5
    out.append(f"shutter shut: boss points reached by a pilot and a Hulk {3 - len(gmiss)} of 3, spawn exits routed with their capsule {len(entries) - len(gbad)} of {len(entries)}, "
               f"entry 0 to boss point 0 {shut if shut is None else round(shut, 1)} m (the shortest loop), no way through the opening: {gok}" + ''.join('\n   BAD ' + i for i in gbad)); ok &= gok
    # The Rigger's hang points: a girder (or the roof truss) overhead at the hook's height, the chain's fall clear, the landing ring
    # (the telegraph's hook shadow and the strike) on reached floor at the exit's level and clear of every proposed hazard.
    rbad = []; rails = 0
    for sid, (bid, hy) in sorted(hangs.items()):
        e = next(e for e in entries if e['id'] == sid); x = e['exit']; ok_r = abs(hy - (e['height'] + RIG_HANG)) < 1e-3 and hy < CEIL
        if bid:
            rails += 1; b = next(b for b in blocks if b['id'] == bid); ok_r &= dist_xz(b, x['x'], x['z']) <= 1.5 and abs(yspan(b)[0] - hy) < 1e-6
        ok_r &= col.clearance(x['x'], x['z'], e['height'] + 0.5, hy - 0.02) >= 0.6
        ring = [(x['x'] + math.cos(k * math.pi / 4) * RIG_RING, x['z'] + math.sin(k * math.pi / 4) * RIG_RING) for k in range(8)]
        ok_r &= all(col.node_at(px, x['y'], pz, 0.36) in reach and col.floor_under(px, pz, x['y']) for px, pz in ring)
        ok_r &= all(math.hypot(max(abs(x['x'] - h['volume']['x']) - h['volume']['sx'] / 2, 0), max(abs(x['z'] - h['volume']['z']) - h['volume']['sz'] / 2, 0)) >= RIG_RING + 0.5 for h in a['ironworks']['hazards'])
        ok_r &= 'rigger' in styles[sid][1] and e['kind'] == 'vent' and e['surface'] == 'ceiling'
        if not ok_r: rbad.append(sid)
    out.append(f'Rigger hang points: {len(hangs)} ({rails} under crane girders, {len(hangs) - rails} under the roof truss): girder overhead within 1.5 m (the chain swings in) at the hook height, chain fall clear 0.6 m, '
               f'the {RIG_RING:g} m landing ring on reached floor and 2 m clear of hazards: {len(hangs) - len(rbad)} of {len(hangs)}' + ''.join('\n   BAD ' + i for i in rbad)); ok &= not rbad and rails >= 4
    # Machine spots: fronts pilot-clear on reached floor, 4 m from every spawn exit on their level, the second chip upstairs.
    mfront = [(x['id'], x['front']) for x in a['ironworks']['machines']]
    mm = min((math.hypot(e['exit']['x'] - p['x'], e['exit']['z'] - p['z']), e['id'], n) for e in entries for n, p in mfront if abs(e['exit']['y'] - p['y']) < 1)
    up = [x for x in a['ironworks']['machines'] if x['kind'] == 'chip' and x['level'] == 1]
    mok = mm[0] >= 4.0 and len(up) == 1 and all(col.node_at(p['x'], p['y'], p['z']) in reach and col.clearance(p['x'], p['z'], p['y'] + 0.35, p['y'] + HEIGHT) >= RADIUS for n, p in mfront)
    out.append('machine spots: fronts on reached floor with the pilot probe, nearest spawn exit %.1f m (%s, %s), the second chip machine upstairs on the walkway: %s' % (mm[0], mm[1], mm[2], mok)); ok &= mok
    rok = all(room_of(e['exit']) for e in entries) and all(room_of(p) for p in BOSS_POINTS + CACHES + ENTRIES_IN)
    out.append(f'every spawn exit, cache, entry and boss point inside a named room: {rok}'); ok &= rok
    vb = all(vol_in_bounds(V, m['bounds']) for V in (d['interact'], d['closure']))
    out.append(f'door volumes inside the bounds (1 cm margin): {vb}'); ok &= vb
    rec = [x['id'] for x in a['anchors']] + [e['id'] for e in entries] + [h['id'] for h in a['ironworks']['hazards']] + [r['id'] for r in a['ironworks']['rooms']] + [x['id'] for x in a['ironworks']['machines']]
    out.append(f'record ids unique {len(set(rec)) == len(rec)}, pass the Expedition id rule {all(id_ok(i) for i in rec)}'); ok &= len(set(rec)) == len(rec) and all(id_ok(i) for i in rec)
    return ok, out, (col, reach, hulk, rh, dist)

LOOPS = {}
def walk(col, start, forbid=None):
    """Walking distances (m) from `start` over the collider's standable cells, 8-connected without corner cutting (as
    expedition_city.walk), steps within 0.36 m; `forbid(node)` keeps nodes out."""
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
def town_checks():
    """The draft's town door in the live town: anchor, approach and interaction on reached street with pilot clearance, the
    returns Hulk-clear and routed from the sanctuary's first slot for a pilot and a Hulk, no town spawn exit within 8 m."""
    out, ok = [], True
    u = json.load(open(os.path.join(RES, 'UnityMaps.json'))); city = next(x for x in u['maps'] if x['id'] == 'expedition-city')
    live = live_manifest(); town = next(a for a in live['areas'] if a['id'] == live['world'])
    col = Collider(city['blocks'], city.get('ramps') or [], city['bounds']); col.build(lambda y: abs(y) < 0.05)
    hulk = Collider(city['blocks'], city.get('ramps') or [], city['bounds']); hulk.build(lambda y: abs(y) < 0.05, HULK_RADIUS, HULK_HEIGHT)
    s0 = town['sanctuary']['slots'][0]; rp = col.flood([col.node_at(s0['x'], 0, s0['z'])]); rh = hulk.flood([hulk.node_at(s0['x'], 0, s0['z'])])
    anchor, rec = town_door(); bad = []
    for name, p, need, heavy in [('anchor', anchor, RADIUS, False), ('approach', rec['approach'], PILOT_CLEAR, True)] + [(f'return{i}', p, HULK_SLOT, True) for i, p in enumerate(rec['returns'])]:
        n = col.node_at(p['x'], 0, p['z']); hn = hulk.node_at(p['x'], 0, p['z'], 0.36)
        c = col.clearance(p['x'], p['z'], 0.35, (HULK_HEIGHT if heavy else HEIGHT) + 0.05)
        if n not in rp or (heavy and hn not in rh) or c < need: bad.append(f"{name} ({p['x']:g}, {p['z']:g}) routed {n in rp}/{hn in rh} clear {c:.2f}")
    out.append(f'town foundry door ({TOWN_DOOR[0]:g}, 0, {TOWN_DOOR[1]:g}) yaw {TOWN_YAW:g}: anchor, approach and 8 returns on the live town street, routed from sanctuary slot 0 (returns and approach for a Hulk too), returns Hulk-clear: {10 - len(bad)} of 10' + ''.join('\n   BAD ' + s for s in bad))
    ok &= not bad
    vb = all(vol_in_bounds(V, city['bounds']) for V in (rec['interact'], rec['closure'])) and col.floor_under(rec['interact']['x'], rec['interact']['z'], 0.0)
    out.append(f'town door volumes inside the town bounds and on its floor: {vb}'); ok &= vb
    near = min((math.hypot(e['exit']['x'] - p['x'], e['exit']['z'] - p['z']), e['id']) for e in town['spawns'] for p in [anchor, rec['approach']] + rec['returns'])
    out.append('nearest town spawn exit to the foundry door, approach or returns: %.1f m (%s)' % near); ok &= near[0] >= 8.0
    sh = next(b for b in city['blocks'] if b['id'] == 'expedition-city-foundry-shutter'); gapx = abs((sh['pos']['x'] + sh['size']['x'] / 2) - (rec['closure']['x'] - rec['closure']['sz'] / 2))
    out.append(f'the closure stands against the sealed shutter (gap {gapx:.2f} m), the shutter and every town block unchanged (the draft adds records only)'); ok &= gapx < 0.05
    return ok, out

def manifest_checks(m):
    out, ok = [], True
    live = live_manifest()
    same_live = {k: v_ for k, v_ in live.items() if k not in ('revision', 'instances', 'areas')} == {k: v_ for k, v_ in m.items() if k not in ('revision', 'instances', 'areas')}
    a_live = {a['id']: a for a in live['areas']}; a_m = {a['id']: a for a in m['areas']}
    same_ward = a_m['exp-ward-zero'] == a_live['exp-ward-zero']
    t_l, t_m = dict(a_live['exp-city']), dict(a_m['exp-city'])
    extra_a = [x for x in t_m['anchors'] if x not in t_l['anchors']]; extra_d = [x for x in t_m['doors'] if x not in t_l['doors']]
    same_town = {k: t_l[k] for k in t_l if k not in ('anchors', 'doors')} == {k: t_m[k] for k in t_m if k not in ('anchors', 'doors')} and all(x in t_m['anchors'] for x in t_l['anchors']) and all(x in t_m['doors'] for x in t_l['doors'])
    out.append(f"draft manifest {m['revision']}: the live {live['revision']} plus exp-foundry in the instance list ({m['instances']}), the town's foundry door (+{len(extra_a)} anchor, +{len(extra_d)} door) and the exp-foundry area; "
               f"Ward Zero's area unchanged {same_ward}, every other town record unchanged {same_town}, top level unchanged {same_live}")
    ok &= same_live and same_ward and same_town and len(extra_a) == 1 and len(extra_d) == 1 and m['instances'] == live['instances'] + [AREA] and m['revision'] != live['revision']
    f = a_m[AREA]; anc = {x['id']: x for x in f['anchors']}
    ok_f = f['role'] == 'instance' and f['map'] == MAP and len(f['doors']) == 1 and anc[f['doors'][0]['anchor']]['building'] == AREA and len(f['doors'][0]['entries']) == 8 and isinstance(f['spawns'], list)
    ok_f &= all(sum(1 for i in f['interactions'] if i['anchor'] == x['id']) == 1 for x in f['anchors'] if x['kind'] == 'chest') and 1 <= len(f['boss']['points']) <= 8
    ok_f &= all(x['id'].startswith(AREA + ':') for x in f['anchors']) and {x['kind'] for x in f['anchors']} <= {'door', 'chest'}
    ok_f &= all(set(s['allow']) <= {'shambler', 'runner', 'spitter', 'brute', 'hulk'} and 1 <= len(s['allow']) <= 5 and s['kind'] in ('door', 'vent', 'drop', 'breach') for s in f['spawns'])
    ok_f &= [g['id'] for g in f.get('gates', [])] == [GATE['id']] and all(g['opens'] == 'boss' for g in f['gates']) and all(not a_.get('gates') or a_['id'] == m['world'] or a_['id'] == AREA for a_ in m['areas'])
    out.append(f'exp-foundry area: one exit with 8 entries, {len(f["anchors"]) - 1} caches each with one interaction, {len(f["boss"]["points"])} boss points, one instance gate opening with its boss ({f["gates"][0]["id"]}), {len(f["spawns"])} own spawns (existing kinds and the four spawn kinds the parser accepts), the ironworks record additive: {ok_f}')
    ok &= ok_f
    ids = [x['id'] for a in m['areas'] for x in a['anchors']] + [g['id'] for a in m['areas'] for g in a.get('gates', [])] + [s['id'] for a in m['areas'] if isinstance(a['spawns'], list) for s in a['spawns']]
    out.append(f'manifest ids: {len(ids)}, unique {len(set(ids)) == len(ids)}, pass the Expedition id rule {all(id_ok(i) for i in ids)}'); ok &= len(set(ids)) == len(ids) and all(id_ok(i) for i in ids)
    hok = f['blockHash'] == block_hash(game_map()); out.append(f'exp-foundry blockHash equals the draft map\'s: {hok} ({f["blockHash"]})'); ok &= hok
    return ok, out

# =====================================================================================================================
# PLAN (PNG): both floors, north up, rooms named, the critical path, spawns by entrance style, records, proposed hazards.
STYLE_C = {'furnace-mouth': (255, 120, 40), 'ash-door': (255, 160, 70), 'coke-oven-door': (230, 90, 50), 'door': (255, 200, 120), 'slag-trench': (200, 120, 255), 'slag-pit': (170, 90, 255),
           'mill-pit': (190, 140, 255), 'vent': (190, 110, 255), 'gantry': (255, 90, 190), 'bunker-chute': (255, 110, 200), 'catwalk-drop': (255, 70, 150), 'slag-bank': (255, 70, 150),
           'crane-bay': (255, 40, 90), 'crane-rail': (120, 230, 255), 'breach': (255, 235, 60)}
def plan(path, graphs, scale=8):
    col, reach, hulk, rh, dist = graphs; m = game_map(); a = area(); d = door_record()
    SX, SZ = 51, 35; PW, PH = 2 * SX * scale, 2 * SZ * scale; W, H = PW + 20, 2 * PH + 212
    cv = Canvas(W, H, bg=(12, 12, 14))
    for panel, yf in ((0, 0.0), (1, L1)):
        ox = 10; oy = 40 + panel * (PH + 30)
        def X(x): return int(round((x + SX) * scale)) + ox
        def Y(z): return int(round((SZ - z) * scale)) + oy
        cv.rect(X(-SX), Y(SZ), X(SX), Y(-SZ), (20, 18, 20))
        for n in col.graph:
            on = abs(n[2] - yf) < 0.05
            if not on and not (panel == 0 and 0.05 < n[2] < 5.5): continue
            x, z = col.cell_xz(n[0], n[1]); c = (58, 52, 50) if n in reach else (120, 40, 50)
            if not on: c = (66, 74, 92)
            cv.rect(X(x - 0.25), Y(z + 0.25), X(x + 0.25), Y(z - 0.25), c)
        for h in a['ironworks']['hazards']:
            V = h['volume']
            if (V['y'] > 0.5) == (panel == 1) or (panel == 0 and V['y'] - V['sy'] / 2 < 1.5):
                cv.poly([(X(x), Y(z)) for x, z in corners(vol_block('', V))], (255, 70, 20), 0.28)
                cv.frame(X(V['x'] - V['sx'] / 2), Y(V['z'] + V['sz'] / 2), X(V['x'] + V['sx'] / 2), Y(V['z'] - V['sz'] / 2), (255, 110, 40))
        for r in m['ramps']: cv.poly([(X(x), Y(z)) for x, z in corners(ramp_block(r))], (90, 170, 230), 0.45)
        for b in sorted(m['blocks'], key=lambda b: yspan(b)[1]):
            y0, y1 = yspan(b)
            if b['id'] in (MAP + '-slab', MAP + '-roof') or '-stair-' in b['id']: continue
            if not (y0 < yf + 3 and y1 > yf + 0.05): continue
            if panel == 0 and b['id'].startswith(MAP + '-deck-') and '-col-' not in b['id']: continue
            if y0 > 10: continue
            c = (150, 112, 74) if y1 - yf <= 1.6 else (122, 126, 140)
            if '-furnace-' in b['id'] or 'reheat' in b['id'] or 'battery' in b['id'] or 'forge-hearth' in b['id']: c = (200, 96, 50)
            if '-runner-' in b['id']: c = (230, 120, 50)
            if '-breach-' in b['id']: c = (255, 235, 60)
            if '-exit-' in b['id']: c = (0, 160, 255)
            if b['id'].startswith(MAP + '-deck-') and '-rail-' in b['id']: c = (220, 190, 60)
            if b['id'].startswith(MAP + '-deck-') and '-rail-' not in b['id'] and '-col-' not in b['id']: c = (96, 100, 112)
            cv.poly([(X(x), Y(z)) for x, z in corners(b)], c)
        for e in entries:
            if (e['exit']['y'] > 3) != (panel == 1): continue
            x, z = e['exit']['x'], e['exit']['z']; c = STYLE_C.get(styles[e['id']][0], (255, 255, 255))
            cv.line(X(e['from']['x']), Y(e['from']['z']), X(x), Y(z), c, 1); cv.disc(X(x), Y(z), 5 if 'hulk' in e['allow'] else 3, c)
        if panel == 0:
            for key, c in (('interact', (80, 220, 255)), ('closure', (255, 255, 120))): cv.poly([(X(x), Y(z)) for x, z in corners(vol_block('', d[key]))], c, 0.6)
            cv.poly([(X(x), Y(z)) for x, z in corners(vol_block('', GATE['closure']))], (255, 60, 60), 0.9)
            for p in ENTRIES_IN:
                cx, cy = X(p['x']), Y(p['z']); fx, fz = forward(p['yaw']); cv.disc(cx, cy, 4, (255, 255, 255)); cv.line(cx, cy, cx + int(fx * 9), cy - int(fz * 9), (255, 255, 255), 2)
            for p in BOSS_POINTS:
                cx, cy = X(p['x']), Y(p['z']); cv.line(cx - 7, cy - 7, cx + 7, cy + 7, (255, 90, 255), 3); cv.line(cx - 7, cy + 7, cx + 7, cy - 7, (255, 90, 255), 3)
            cv.frame(X(ARENA['x'] - ARENA['sx'] / 2), Y(ARENA['z'] + ARENA['sz'] / 2), X(ARENA['x'] + ARENA['sx'] / 2), Y(ARENA['z'] - ARENA['sz'] / 2), (255, 90, 255), 2)
            # the critical path (entry -> casting floor -> north or south loop -> converter hall)
            for pts, c in (([(46, 0), (36, 0), (20, 6), (0, 10), (-17, 18), (-24, 18), (-33, 16), (-32, 12), (-35, 4)], (120, 255, 160)),
                           ([(46, 0), (36, 0), (20, -6), (4, -12), (4, -18), (-10, -18.5), (-24, -20), (-32, -16), (-32, -12), (-35, -4)], (120, 255, 160))):
                for (x0_, z0_), (x1_, z1_) in zip(pts, pts[1:]): cv.line(X(x0_), Y(z0_), X(x1_), Y(z1_), c, 3)
        for i, c in enumerate(CACHES):
            if (c['y'] > 3) == (panel == 1): cv.rect(X(c['x']) - 6, Y(c['z']) - 6, X(c['x']) + 6, Y(c['z']) + 6, (255, 200, 40)); cv.text(X(c['x']) + 8, Y(c['z']) - 5, f'C{i}', (255, 200, 40))
        for x in a['ironworks']['machines']:
            if (x['y'] > 3) == (panel == 1): cv.rect(X(x['x']) - 5, Y(x['z']) - 5, X(x['x']) + 5, Y(x['z']) + 5, (120, 255, 200)); cv.text(X(x['x']) - 34, Y(x['z']) - 5, x['kind'].upper(), (120, 255, 200))
        for i, n, x0, x1, z0, z1, lv, role in ROOMS:
            if (panel == 1) and L1 not in lv: continue
            t = n.upper().replace(' AND ORE BRIDGE', '').replace(' AND CHARGING DECK', ' / DECK').replace(' AND DROP FORGE', ' / FORGE')
            tx = min(max(X((x0 + x1) / 2) - text_width(t) // 2, X(x0) + 4), X(x1) - text_width(t) - 4); cv.text(tx, Y(z1) + 6, t, (235, 235, 240))
        cv.text(ox, oy - 16, 'GROUND FLOOR  Y 0' if panel == 0 else 'UPPER RING AND WALKWAYS  Y 6', (200, 220, 235))
    cv.text(10, 8, 'CITY IRONWORKS  EXPEDITION INSTANCE  BLOCKOUT DRAFT (NOT LIVE)  100 X 68 M  NORTH UP  1 PX = %d CM' % (100 // scale), (255, 214, 170))
    lx, ly = 10, H - 134
    leg = [('EXIT DOOR', (0, 160, 255)), ('INTERACT', (80, 220, 255)), ('CLOSURE', (255, 255, 120)), ('ENTRY SLOT', (255, 255, 255)), ('BOSS POINT / ARENA', (255, 90, 255)),
           ('CACHE', (255, 200, 40)), ('MACHINE SPOT', (120, 255, 200)), ('SHUTTER (OPENS WITH THE BOSS)', (255, 60, 60)), ('HAZARD (PROPOSAL)', (255, 110, 40)), ('CRITICAL PATH', (120, 255, 160)), ('STAIR', (90, 170, 230)),
           ('FURNACE / HOT MASS', (200, 96, 50)), ('RUNNER CURB', (230, 120, 50)), ('RAIL', (220, 190, 60)), ('BREACH', (255, 235, 60))]
    leg += [(k.upper().replace('-', ' '), c) for k, c in STYLE_C.items() if k != 'breach']
    for label, c in leg:
        cv.rect(lx, ly, lx + 12, ly + 10, c); lx = cv.text(lx + 18, ly, label, (210, 214, 220)) + 18
        if lx > W - 220: lx, ly = 10, ly + 22
    cv.save(path)

def ascii_plan():
    rows = []
    for zz in range(int(Z1) - 1, int(Z0) - 1, -1):
        row = ''
        for xx in range(int(X0), int(X1)):
            x, z = xx + 0.5, zz + 0.5; ch = '.'
            for b in blocks:
                if b['id'] in (MAP + '-slab', MAP + '-roof') or yspan(b)[0] > 5.5: continue
                if dist_xz(b, x, z) <= 0: ch = b['_ch']
            row += ch
        rows.append(row)
    return '\n'.join(rows)

def main():
    ap = argparse.ArgumentParser(); ap.add_argument('--map'); ap.add_argument('--layout'); ap.add_argument('--look'); ap.add_argument('--png'); ap.add_argument('--ascii', action='store_true')
    ap.add_argument('--fixtures', action='store_true'); a = ap.parse_args()
    if a.fixtures:
        for k, name in FIXTURES.items():
            if getattr(a, k) is None: setattr(a, k, os.path.join(TESTS, name))
    ok_i, rep_i, graphs = checks(); ok_t, rep_t = town_checks(); m = manifest(); ok_m, rep_m = manifest_checks(m)
    print(f'== {MAP} ({AREA}): City Ironworks blockout draft (NOT LIVE: Resources/ExpeditionLayout.json stays {LIVE_REVISION})'); print('\n'.join(rep_i)); print('instance checks: ' + ('pass' if ok_i else 'FAIL'))
    print('== the town side (exp-city, the live town map)'); print('\n'.join(rep_t)); print('town door checks: ' + ('pass' if ok_t else 'FAIL'))
    print('== draft manifest'); print('\n'.join(rep_m)); print('draft manifest checks: ' + ('pass' if ok_m else 'FAIL'))
    if a.ascii: print(ascii_plan())
    for path, data in ((a.map, draft()), (a.layout, m), (a.look, look_set())):
        if path:
            with open(path, 'w') as f: json.dump(data, f, indent=None, separators=(',', ':'), sort_keys=False)
            print('wrote', path)
    if a.png: plan(a.png, graphs); print('wrote', a.png)
    sys.exit(0 if ok_i and ok_t and ok_m else 1)

if __name__ == '__main__':
    main()
