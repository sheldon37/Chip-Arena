# Polaris Station M1 (polar, 64x52, a research station on the ice shelf at polar night: the radome hall, station modules, snowfield flanks, two garages).
# Coordinates: x east, z north, y up, metres; Unity frame. Point symmetric: (x,z)->(-x,-z). Team A west garage, team B east garage; FFA 8.
# usage: python3 docs/arenas/scripts/polar_m1.py [--out DIR] [--json PATH]   (stdout: plan + checks, exit 1 if any check fails; --json writes the
#        draft only; --out writes polar_m1.json, draft map data in the UnityMaps.json shape, and polar_m1.png, a top-down plan, 8 px per metre)
# A draft for Astra (docs/arenas/NEW-STAGES.md), NOT wired into the game. Ids carry the prefixes the presentation keys its looks and kit dressing on:
# polar-hall-* (radome hall walls, porches, pedestal, racks, consoles), polar-radome, polar-deck-*, polar-rail-*, polar-stair-*, polar-module-* (station
# modules = kit containers on skids), polar-tank-*, polar-drill-*, polar-hut-*, polar-shed-*, polar-melter-*, polar-snowcat-*, polar-bowser-*,
# polar-garage-*, polar-crate-*, polar-berm-*, polar-bank-*; tokens n s e w a b swap in mirrored partners. Geometry and spawns become sim-owned once
# integrated. No hazards in M1.
import math, json, itertools, sys, os, zlib, struct
X0, X1, Z0, Z1 = -34, 34, -28, 28           # slab; play bounds x +-32, z +-26 inside the 2 m snow bank
BOUNDS = {'minX': -32, 'maxX': 32, 'minZ': -26, 'maxZ': 26, 'ceiling': 18, 'killY': -6}
CAP = .42                                    # capsule radius
els = []   # dict: id, mat, role, box (x0,x1,y0,y1,z0,z1), ch, hp
SWAP = {'n': 's', 's': 'n', 'w': 'e', 'e': 'w', 'a': 'b', 'b': 'a'}
def v(x, y, z): return {'x': x, 'y': y, 'z': z}
def mirror_id(i): return '-'.join(SWAP.get(t, t) for t in i.split('-'))
def add(i, x0, x1, y0, y1, z0, z1, ch, mat='stone', role='static', hp=0, mirror=True):
    els.append(dict(id=i, mat=mat, role=role, box=(x0, x1, y0, y1, z0, z1), ch=ch, hp=hp))
    if mirror:
        m = mirror_id(i); assert m != i, 'mirror of %s keeps its id' % i
        els.append(dict(id=m, mat=mat, role=role, box=(-x1, -x0, y0, y1, -z1, -z0), ch=ch, hp=hp))
def wall_x(i, x0, x1, y0, y1, z0, z1, openings, ch, **k):
    cuts = [x0] + [c for o in sorted(openings) for c in o] + [x1]
    for j in range(0, len(cuts), 2):
        if cuts[j + 1] > cuts[j]: add('%s-%d' % (i, j // 2), cuts[j], cuts[j + 1], y0, y1, z0, z1, ch, **k)
def wall_z(i, x0, x1, y0, y1, z0, z1, openings, ch, **k):
    cuts = [z0] + [c for o in sorted(openings) for c in o] + [z1]
    for j in range(0, len(cuts), 2):
        if cuts[j + 1] > cuts[j]: add('%s-%d' % (i, j // 2), x0, x1, y0, y1, cuts[j], cuts[j + 1], ch, **k)
def stair(i, xa, xb, z0, z1, ya, yb, steps, mirror=True, axis='x'):
    # a flight of solid steps from (xa, ya) to (xb, yb); along x (z0..z1 wide) or along z (then xa/xb are z, z0/z1 are x)
    run = (xb - xa) / steps; rise = (yb - ya) / steps
    for k in range(steps):
        a, b = sorted((xa + run * k, xa + run * (k + 1)))
        y0 = ya if ya == 0 and yb > 0 else ya + rise * k - .2      # ground flights are solid; elevated flights are 20 cm slabs
        y1 = ya + rise * (k + 1)
        if axis == 'x': add('%s-%02d' % (i, k), a, b, max(0, y0), y1, z0, z1, '/', mat='metal', mirror=mirror)
        else: add('%s-%02d' % (i, k), z0, z1, max(0, y0), y1, a, b, '/', mat='metal', mirror=mirror)
PILOT = []   # pilot-only blocking columns (bots' planner) above rails <= 1.2 m over a drop and above unstandable tops
def pilot(i, x0, x1, z0, z1, bottom, mirror=True):
    PILOT.append(dict(id=i, center=v(round((x0 + x1) / 2, 3), 0, round((z0 + z1) / 2, 3)), size=v(round(x1 - x0, 3), 0, round(z1 - z0, 3)), bottom=bottom))
    if mirror: PILOT.append(dict(id=mirror_id(i), center=v(round(-(x0 + x1) / 2, 3), 0, round(-(z0 + z1) / 2, 3)), size=v(round(x1 - x0, 3), 0, round(z1 - z0, 3)), bottom=bottom))

FLOOR = dict(id='polar-floor', mat='foundation', role='static', box=(-34, 34, -.5, 0, -28, 28), ch='.', hp=0)
# ---- snow bank: a 2 m stepped drift all round (foot 1 m, ridge 1 m behind it), outside the play bounds, >= 5 m, tops unstandable
for k, (x0, x1, hf, hr) in enumerate(((-34, -12, 5.4, 6.6), (-12, 9, 5.0, 6.0), (9, 34, 5.6, 7.0))):
    add('polar-bank-n-%d-foot' % k, x0, x1, 0, hf, 26, 27, '#'); add('polar-bank-n-%d-ridge' % k, x0, x1, 0, hr, 27, 28, '#')
for k, (z0, z1, hf, hr) in enumerate(((-26, -9, 5.2, 6.4), (-9, 9, 5.8, 7.2), (9, 26, 5.0, 6.2))):
    add('polar-bank-w-%d-foot' % k, -33, -32, 0, hf, z0, z1, '#'); add('polar-bank-w-%d-ridge' % k, -34, -33, 0, hr, z0, z1, '#')
pilot('polar-top-bank-n', -34, 34, 26, 28, 4.9); pilot('polar-top-bank-w', -34, -32, -26, 26, 4.9)

# ---- the radome hall (centre): an octagon 12 m across, 0.4 m walls (the diagonal sides are 5 overlapping steps), four 2.0 x 2.4 m doors
H = 5.6                                      # eave: 3.2 m mezzanine + 2.4 m headroom (the spec's 4.5 m leaves 1.3 m over the deck)
wall_x('polar-hall-wall-n', -2.5, 2.5, 0, H, 5.6, 6.0, [(-1, 1)], 'H', mat='metal')
add('polar-hall-wall-n-lintel', -1, 1, 2.4, H, 5.6, 6.0, 'H', mat='metal')
wall_z('polar-hall-wall-w', -6.0, -5.6, 0, H, -2.5, 2.5, [(-1, 1)], 'H', mat='metal')
add('polar-hall-wall-w-lintel', -6.0, -5.6, 2.4, H, -1, 1, 'H', mat='metal')
D, T = 3.5 / 5, .4
for k in range(5):
    add('polar-hall-wall-n-e-%d' % k, 2.5 - T + k * D, 2.5 + (k + 1) * D, 0, H, 6.0 - (k + 1) * D - T, 6.0 - k * D, 'H', mat='metal')
    add('polar-hall-wall-n-w-%d' % k, -(2.5 + (k + 1) * D), -(2.5 - T + k * D), 0, H, 6.0 - (k + 1) * D - T, 6.0 - k * D, 'H', mat='metal')
add('polar-hall-roof', -6.0, 6.0, H, H + .4, -6.0, 6.0, 'R', mat='metal', mirror=False)
add('polar-radome', -4.5, 4.5, H + .4, 11.0, -4.5, 4.5, 'R', mat='metal', mirror=False)          # the dome: landmark, unstandable
pilot('polar-top-hall', -6.4, 6.4, -6.4, 6.4, H - .1, mirror=False)
# windbreak porches (2.4) flank each door outside: CQB cover at the doors, and they cut sightlines grazing the hall
for i_, (a_, b_, c_, d_) in (('w-n', (-8.0, -6.0, 1.6, 2.0)), ('w-s', (-8.0, -6.0, -2.0, -1.6)), ('n-w', (-2.0, -1.6, 6.0, 8.0)), ('n-e', (1.6, 2.0, 6.0, 8.0))):
    add('polar-hall-porch-' + i_, a_, b_, 0, 2.4, c_, d_, 'H', mat='metal'); pilot('polar-top-hall-porch-' + i_, a_, b_, c_, d_, 2.3)
# interior: the antenna pedestal (3.0) under a bridge (the mezzanine, 3.2) spanning west-east over the W and E doors. Stair, pedestal and the
# mirrored stair split the floor into two rooms (N+E doors, S+W doors), each with its own stair up to the shared bridge (point symmetry needs two)
add('polar-hall-pedestal', -1.0, 1.0, 0, 3.0, -1.0, 1.0, 'P', mat='metal', mirror=False)
add('polar-deck-hall-bridge', -5.6, 5.6, 3.0, 3.2, -1.0, 1.0, 'g', mat='metal', mirror=False)
stair('polar-stair-hall-n', 5.0, 1.0, -2.6, -1.0, 0, 3.2, 16, axis='z')
add('polar-rail-hall-n-w', -5.6, -2.6, 3.2, 4.3, 0.8, 1.0, 'r', mat='metal'); add('polar-rail-hall-n-e', -1.0, 5.6, 3.2, 4.3, 0.8, 1.0, 'r', mat='metal')
pilot('polar-top-rail-hall-n-w', -5.6, -2.6, 0.8, 1.0, 4.2); pilot('polar-top-rail-hall-n-e', -1.0, 5.6, 0.8, 1.0, 4.2)
add('polar-hall-rack-n-w', -3.4, -2.6, 0, H, 2.6, 4.2, 'H', mat='metal')                        # equipment racks to the roof: seal the stair foot's corner
add('polar-hall-console-n-e', 1.6, 3.6, 0, 1.1, 2.4, 3.0, 'o', mat='metal')                     # console bank: half cover

# ---- mid ring: station modules on skids (top 2.9), fuel tank pairs and gates of 3.2 m or more around the courtyard (x +-14.2, z +-10.4)
# north side (mirrored to the south side): a 40 ft module, the 20 ft module with the roof walkway (centred: neutral), the tank pair;
# NW corner sealed, NE corner open; gates x -4.4..-1.2 and 4.8..8.0
add('polar-module-n-w', -16.6, -4.4, 0, 2.9, 10.4, 12.8, 'k', mat='metal'); pilot('polar-top-module-n-w', -16.6, -4.4, 10.4, 12.8, 2.8)
add('polar-module-n', -1.2, 4.8, 0, 2.9, 10.4, 12.8, 'K', mat='metal')                             # walkable roof (2.9 m drops: no rails)
stair('polar-stair-module-n', 17.2, 12.8, 0.0, 1.6, 0, 2.9, 15, axis='z')                       # from the north flank, rising south onto the roof
add('polar-tank-n-e-0', 8.0, 10.6, 0, 3.0, 10.2, 12.8, 'T', mat='metal'); add('polar-tank-n-e-1', 11.0, 13.6, 0, 3.0, 10.2, 12.8, 'T', mat='metal')
pilot('polar-top-tank-n-e', 8.0, 13.6, 10.2, 12.8, 2.9)
# west side (mirrored east): a 20 ft module closing the NW corner, the west gate (z 0.4..4.4), the tank pair; the SW corner open
add('polar-module-w', -16.6, -14.2, 0, 2.9, 4.4, 10.4, 'k', mat='metal'); pilot('polar-top-module-w', -16.6, -14.2, 4.4, 10.4, 2.8)
add('polar-tank-w-0', -16.8, -14.2, 0, 3.0, -2.2, 0.4, 'T', mat='metal'); add('polar-tank-w-1', -16.8, -14.2, 0, 3.0, -5.0, -2.6, 'T', mat='metal')
pilot('polar-top-tank-w', -16.8, -14.2, -5.0, 0.4, 2.9)
# courtyard cover (west, mirrored east): a berm and crates in front of the hall's W door
def berm(i, x0, x1, z0, z1, top=1.1):                # stepped snow berm: a 0.4 m apron (walk-over) and the crest (half cover)
    add(i + '-apron', x0 - .4, x1 + .4, 0, .4, z0 - .4, z1 + .4, 'b'); add(i + '-crest', x0, x1, .4, top, z0, z1, 'b')
add('polar-hut-w', -11.8, -9.4, 0, 2.6, -1.4, 1.4, 'h', mat='metal'); pilot('polar-top-hut-w', -11.8, -9.4, -1.4, 1.4, 2.5)      # instrument hut: full cover
berm('polar-berm-w', -12.4, -9.4, -5.4, -4.2)
add('polar-crate-w-0', -8.8, -7.6, 0, 1.2, 3.4, 4.6, 'x', mat='wood', role='cover', hp=34)
add('polar-crate-w-1', -8.4, -7.2, 0, 2.4, 7.6, 8.8, 'x', mat='wood', role='cover', hp=34)
add('polar-melter-w', -11.2, -9.2, 0, 2.4, 4.8, 6.4, 'O', mat='metal'); pilot('polar-top-melter-w', -11.2, -9.2, 4.8, 6.4, 2.3)   # snow melter: full cover
add('polar-hut-n-e', 9.0, 11.4, 0, 2.6, 7.2, 9.6, 'h', mat='metal'); pilot('polar-top-hut-n-e', 9.0, 11.4, 7.2, 9.6, 2.5)      # closes the north road's east end

# ---- drill tower (north flank, mirrored south): the drill house (solid, 3.4) under a scaffold deck at 3.6, mast above, pipe rack behind,
#      stair from the west (A's flank zone; the south tower's from the east); the deck is the exposed power position (1.1 m rails only)
DX0, DX1, DZ0, DZ1 = -9.4, -5.0, 12.8, 17.2
add('polar-drill-n-house', DX0, DX1, 0, 3.4, DZ0, DZ1, 'd', mat='metal')                         # its top is the deck
add('polar-deck-drill-n', DX0, DX1, 3.4, 3.6, DZ0, DZ1, 'D', mat='metal')
add('polar-drill-n-mast', DX0 + 1.4, DX0 + 2.6, 3.6, 12.0, DZ1 - 1.6, DZ1 - .4, 'M', mat='metal'); pilot('polar-top-drill-n-mast', DX0 + 1.4, DX0 + 2.6, DZ1 - 1.6, DZ1 - .4, 11.9)
add('polar-drill-n-piperack', DX0 + 1.0, DX0 + 3.4, 0, 2.4, DZ1, DZ1 + 3.2, 'O', mat='metal'); pilot('polar-top-drill-n-piperack', DX0 + 1.0, DX0 + 3.4, DZ1, DZ1 + 3.2, 2.3)
stair('polar-stair-drill-n', DX0 - 4.5, DX0, DZ0 + .4, DZ0 + 2.0, 0, 3.6, 18)
for s_, (a_, b_, c_, d_) in (('s', (DX0, DX1, DZ0, DZ0 + .2)), ('w', (DX0, DX0 + .2, DZ0 + 2.0, DZ1)), ('n', (DX0 + .2, DX1, DZ1 - .2, DZ1)), ('e', (DX1 - .2, DX1, DZ0 + .2, DZ1 - .2))):
    add('polar-rail-drill-n-%s' % s_, a_, b_, 3.6, 4.7, c_, d_, 'r', mat='metal'); pilot('polar-top-rail-drill-n-%s' % s_, a_, b_, c_, d_, 4.6)

# ---- snowfield flanks (north, mirrored south): a slalom of full cover alternately from the bank and from the ring (depot module, drill tower,
#      snowcat, field module) with berms (half cover) and the sled cargo line (breakable crates) between
add('polar-module-n-w-depot', -23.4, -21.0, 0, 2.9, 19.6, 25.6, 'k', mat='metal'); pilot('polar-top-module-n-w-depot', -23.4, -21.0, 19.6, 25.6, 2.8)
add('polar-module-n-e-field', 15.4, 17.8, 0, 2.9, 13.2, 19.2, 'k', mat='metal'); pilot('polar-top-module-n-e-field', 15.4, 17.8, 13.2, 19.2, 2.8)
add('polar-snowcat-n', 5.0, 7.6, 0, 2.4, 19.0, 24.0, 'V', mat='metal'); add('polar-snowcat-n-blade', 4.6, 8.0, 0, 0.9, 18.4, 19.0, 'o', mat='metal')
add('polar-snowcat-n-sled', 5.2, 7.4, 0, 2.4, 24.0, 25.6, 'V', mat='wood')                                   # its cargo sled, tarped: static
pilot('polar-top-snowcat-n', 5.0, 7.6, 19.0, 25.6, 2.3)
berm('polar-berm-n-w', -19.0, -15.0, 16.0, 17.4)
berm('polar-berm-n-w-corner', -30.0, -26.0, 12.6, 14.0, 1.0)              # NW corner field (mirror: SE)
berm('polar-berm-n-e-corner', 24.0, 28.0, 13.0, 14.4, 1.2)                # NE corner field (mirror: SW)
berm('polar-berm-n', -2.0, 2.0, 18.4, 19.8, 1.0)
berm('polar-berm-n-e', 11.0, 15.0, 15.6, 17.0, 1.2)
for k, (x0, x1, h) in enumerate(((-19.4, -17.0, 1.2), (-14.6, -13.4, 2.4), (-11.0, -8.6, 1.2))):     # sled cargo line
    add('polar-crate-n-w-%d' % k, x0, x1, 0, h, 22.0, 23.2, 'x', mat='wood', role='cover', hp=34)

# ---- bases: team A's garage (west; B's is the mirror), an open-sided shed with 3.6 m eaves, open to the east in three bays; generator hut
add('polar-garage-a-wall-w', -32.0, -31.6, 0, 3.6, -7.0, 7.0, 'G', mat='metal')
add('polar-garage-a-wall-n', -31.6, -23.6, 0, 3.6, 6.6, 7.0, 'G', mat='metal')
add('polar-garage-a-wall-s', -31.6, -23.6, 0, 3.6, -7.0, -6.6, 'G', mat='metal')
add('polar-garage-a-post-0', -24.0, -23.6, 0, 3.6, -2.6, -2.2, 'C', mat='metal'); add('polar-garage-a-post-1', -24.0, -23.6, 0, 3.6, 2.2, 2.6, 'C', mat='metal')
add('polar-garage-a-roof', -32.0, -23.2, 3.6, 3.9, -7.4, 7.4, 'R', mat='metal'); pilot('polar-top-garage-a', -32.0, -23.2, -7.4, 7.4, 3.5)
add('polar-garage-a-header', -24.0, -23.6, 2.4, 3.6, -6.6, 6.6, 'G', mat='metal')                       # door header: bays 2.4 m clear, so elevated
#   positions (module roofs, drill decks) cannot see spawns 6 m deep; the bays stay legal doors (>= 2.4 m)
add('polar-garage-a-bench', -31.6, -30.8, 0, 1.0, -5.6, -2.0, 'o', mat='wood')
add('polar-garage-a-loader', -26.2, -23.8, 0, 2.4, -6.0, -3.4, 'V', mat='metal')                        # parked in the bay mouths: screen the diagonals
add('polar-garage-a-tractor', -26.2, -23.8, 0, 2.4, 3.4, 6.0, 'V', mat='metal')
add('polar-garage-a-toolwall', -27.0, -26.2, 0, 2.4, -1.0, 1.0, 'O', mat='metal')                  # tool wall behind the middle bay
add('polar-garage-a-drums', -31.4, -30.2, 0, 1.2, 1.8, 3.0, 'x', mat='metal', role='cover', hp=150)
add('polar-hut-a', -21.0, -16.8, 0, 2.6, -1.8, 1.8, 'h', mat='brick'); pilot('polar-top-hut-a', -21.0, -16.8, -1.8, 1.8, 2.5)       # generator hut: screens the middle bay
# windbreak fences (2.4) continue the side walls: the forecourt opens north (2.4 m gap), east (the W gate) and south (the ring's SW corner)
add('polar-garage-a-fence-n', -23.6, -19.0, 0, 2.4, 6.6, 7.0, 'G', mat='wood'); pilot('polar-top-garage-a-fence-n', -23.6, -19.0, 6.6, 7.0, 2.3)
add('polar-garage-a-fence-s', -23.6, -19.0, 0, 2.4, -7.0, -6.6, 'G', mat='wood'); pilot('polar-top-garage-a-fence-s', -23.6, -19.0, -7.0, -6.6, 2.3)
add('polar-shed-a', -23.4, -20.4, 0, 2.6, 8.4, 11.2, 'h', mat='metal'); pilot('polar-top-shed-a', -23.4, -20.4, 8.4, 11.2, 2.5)        # fuel shed
add('polar-bowser-a', -21.6, -19.0, 0, 2.4, -11.4, -8.4, 'V', mat='metal'); pilot('polar-top-bowser-a', -21.6, -19.0, -11.4, -8.4, 2.3)   # fuel bowser on tracks

# ---- hazards: none in M1 (optional later: geothermal steam vents, kind `vent`)
HAZ = []
def hazard_rects(): return []

# ---- spawns (yaw from +z toward +x, facing the origin as today); every spawn stands on the ground (floor 0, pos y 1.1), so the LOS segments are
#      level, though the test is 3D (a box blocks only where the segment passes through it in x, z and y)
teamA = [(-28.2, 0.0), (-30.2, 4.6), (-29.8, -2.4), (-29.4, 2.4)]      # behind the tool wall and the tractor: hidden from the roofs and decks
teamB = [(-x + 0.0, -z + 0.0) for x, z in teamA]
ffa_names = ['A1', 'B1', 'f', 'F', 'g', 'G', 'h', 'H']
ffa = [teamA[0], teamB[0], (-28.0, 21.0), (28.0, -21.0), (1.6, 24.4), (-1.6, -24.4), (27.5, 21.0), (-27.5, -21.0)]
def yaw(p): return (math.degrees(math.atan2(-p[0], -p[1])) + 360) % 360
# named walkable levels the reachability check must reach on foot from A1: (name, x, z, top y)
LEVELS = [('hall bridge', 0.0, 0.0, 3.2), ('module roof n', 2.4, 11.6, 2.9), ('module roof s', -2.4, -11.6, 2.9),
          ('drill deck n', -7.2, 15.0, 3.6), ('drill deck s', 7.2, -15.0, 3.6)]
DOORS = {'n': (0, 5.8, 'z'), 's': (0, -5.8, 'z'), 'e': (5.8, 0, 'x'), 'w': (-5.8, 0, 'x')}

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
    for e in sorted(els, key=lambda e: (e['box'][2] < 2.0, e['box'][3])):     # overhead first (decks, rails, mast); lintels, headers, roofs skipped
        b = e['box']
        if e['ch'] == 'R' or (b[2] >= 2.0 and e['ch'] not in 'gDrM'): continue
        rect(b[0], b[1], b[4], b[5], e['ch'])
    for i, (x, z) in enumerate(teamA): put(x, z, str(i + 1)); put(-x, -z, str(i + 5))
    for nm, (x, z) in zip(ffa_names[2:], ffa[2:]): put(x, z, nm)
    out = ['legend: # bank  H hall walls/porches/racks  P pedestal  g bridge (3.2)  D drill deck (3.6) over d house  M mast  r rails  / stairs',
           '        K module, walkable roof (2.9)  k module  T fuel tanks  h huts/sheds  V vehicles  O full cover  o half cover  x crates  b berms',
           '        G garage walls/fences  C posts  1-4 team A  5-8 team B  f g h F G H other FFA spawns (A1 = 1, B1 = 5)',
           '         x=-34' + ' ' * 30 + 'x=0' + ' ' * 31 + 'x=+33']
    for r in range(R): z = Z1 - 2 * r; out.append(f'{z-2:+4d}..{z:+3d} ' + ''.join(g[r]))
    return '\n'.join(out)

# ---- line of sight: 3D segment vs box (slab test); for spawns only boxes topping 2.0 m above the lower eye's floor block, never breakables (cover)
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
def visible(a, b, floor=0.0, blockers=None):
    return not any(seg_box(a, b, e['box']) for e in (blockers if blockers is not None else els) if e['box'][3] >= floor + 2.0 and e['role'] != 'cover')
def check(pairs, label):
    bad = []
    for (na, a), (nb, b) in pairs:
        n = sum(1 for dx, dz, ex, ez in itertools.product((-1, 0, 1), repeat=4) for eye in (1.2, 1.6, 1.9)
                if visible((a[0] + dx, eye, a[1] + dz), (b[0] + ex, eye, b[1] + ez)))
        if n: bad.append(f'{na}-{nb} ({n}/243 variants open, {math.dist(a, b):.1f} m)')
    print(f'{label}: {len(pairs)} pairs, {len(pairs) - len(bad)} fully blocked under +-1 m jitter x 3 eye heights')
    for s in bad: print('   OPEN', s)
    return len(bad)
def dist_rect(p, r): dx = max(r[0] - p[0], 0, p[0] - r[1]); dz = max(r[2] - p[1], 0, p[1] - r[3]); return math.hypot(dx, dz)

# ---- reachability: a 0.25 m raster of standable tops (floor + every block top), capsule ring and 1.8 m headroom, |dy| <= 0.25 links
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
        if abs(cx) > BOUNDS['maxX'] - CAP: continue
        for j in range(nz):
            cz = BOUNDS['minZ'] + (j + .5) * CELL
            if abs(cz) > BOUNDS['maxZ'] - CAP: continue
            near = tiles.get((int(math.floor(cx)), int(math.floor(cz))), [])
            over = [b for b in near if b[0] < cx < b[1] and b[4] < cz < b[5]]
            tops = []
            for t in sorted({0.0} | {round(b[3], 3) for b in over}):
                if any(b[2] < t + 1.8 - 1e-3 and b[3] > t + 1e-3 for b in over): continue
                if any(b[0] - CAP < cx < b[1] + CAP and b[4] - CAP < cz < b[5] + CAP and b[2] < t + 1.8 and b[3] > t + .45 for b in near): continue
                tops.append(t)
            if tops: stand[(i, j)] = tops
    def cell(x, z): return (int((x - BOUNDS['minX']) / CELL), int((z - BOUNDS['minZ']) / CELL))
    s0 = cell(*teamA[0]); start = (s0, 0.0); seen = {start}; todo = [start]
    while todo:
        (i, j), t = todo.pop()
        for di, dj in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            n = (i + di, j + dj)
            for u in stand.get(n, ()):
                if abs(u - t) <= .25 + 1e-3 and (n, u) not in seen: seen.add((n, u)); todo.append((n, u))
    return stand, seen, cell

def door_points(k):
    x, z, ax = DOORS[k]
    return [(x, y, z + o) if ax == 'x' else (x + o, y, z) for o in (-.7, 0, .7) for y in (1.0, 1.8)]
def hall_inside(x, z): return abs(x) < 5.6 and abs(z) < 5.6 and abs(x) + abs(z) < 7.9

def unity_json():
    def block(e):
        x0, x1, y0, y1, z0, z1 = e['box']
        return {'id': e['id'], 'material': e['mat'], 'pos': v(round((x0 + x1) / 2, 3) + 0.0, round((y0 + y1) / 2, 3), round((z0 + z1) / 2, 3) + 0.0),
                'size': v(round(x1 - x0, 3), round(y1 - y0, 3), round(z1 - z0, 3)), 'rotation': {'x': 0, 'y': 0, 'z': 0, 'w': 1}, 'role': e['role'], 'mass': 26, 'hp': e['hp']}
    sp = lambda p: {'pos': v(p[0], 1.1, p[1]), 'yaw': round(yaw(p), 3)}
    return {'schema': 1, 'materials': [],
            'maps': [{'id': 'polar', 'name': 'Polaris Station', 'tag': 'Polaris Station, day 0 of 120. Nobody is coming.', 'theme': 'polar',
                      'music': 'polar-station', 'ambience': 'polar-wind', 'look': 'polar-aurora', 'sky': 0x0B1426, 'floor': 0xD8E2EC, 'accent': 0x5CF2A8,
                      'rotation': False, 'bounds': BOUNDS,
                      'spawns': {'ffa': [sp(p) for p in ffa], 'teams': [{'side': 0, 'points': [sp(p) for p in teamA]}, {'side': 1, 'points': [sp(p) for p in teamB]}]},
                      'blocks': [block(FLOOR)] + [block(e) for e in els], 'segments': [], 'pilotBlocks': PILOT, 'hazards': HAZ, 'nav': {'nodes': [], 'links': []}}]}

def png(path, scale=8):
    W, H_ = (X1 - X0) * scale, (Z1 - Z0) * scale; img = bytearray(b'\x18\x1a\x1e' * W * H_)
    def fill(x0, x1, z0, z1, c, alpha=1.0):
        i0, i1 = int((x0 - X0) * scale), int((x1 - X0) * scale); j0, j1 = int((Z1 - z1) * scale), int((Z1 - z0) * scale)
        for j in range(max(0, j0), min(H_, j1)):
            for i in range(max(0, i0), min(W, i1)):
                o = (j * W + i) * 3
                for k in range(3): img[o + k] = int(img[o + k] * (1 - alpha) + c[k] * alpha)
    def frame(x0, x1, z0, z1, c):
        for a, b, cc, d in ((x0, x1, z1 - .15, z1), (x0, x1, z0, z0 + .15), (x0, x0 + .15, z0, z1), (x1 - .15, x1, z0, z1)): fill(a, b, cc, d, c)
    fill(X0, X1, Z0, Z1, (150, 160, 174))
    for m in range(-32, 33, 4): fill(m - .02, m + .02, Z0, Z1, (140, 150, 164))                    # 4 m grid
    for m in range(-24, 25, 4): fill(X0, X1, m - .02, m + .02, (140, 150, 164))
    col = {'#': (225, 232, 240), 'H': (70, 78, 92), 'P': (40, 46, 56), 'g': (120, 150, 170), 'r': (230, 200, 60), '/': (190, 190, 196), 'K': (176, 72, 52),
           'k': (150, 90, 60), 'T': (90, 120, 110), 'D': (210, 150, 60), 'O': (100, 100, 112), 'd': (100, 100, 112), 'C': (50, 50, 60), 'M': (240, 120, 40), 'b': (200, 210, 222),
           'V': (220, 120, 30), 'o': (130, 125, 110), 'x': (180, 130, 75), 'G': (96, 104, 118), 'h': (120, 90, 80)}
    for e in sorted(els, key=lambda e: e['box'][3]):
        b = e['box']
        if e['ch'] == 'R' or (b[2] >= 2.0 and e['ch'] not in 'gDrM'): continue             # roofs, lintels, headers: not drawn
        c = col.get(e['ch'], (128, 128, 128))
        if e['ch'] == 'b' and b[3] > .5: c = (238, 242, 248)
        fill(b[0], b[1], b[4], b[5], c, .6 if b[2] >= 2.0 else 1.0)
    for e in els:
        if e['ch'] == 'R' and e['id'] != 'polar-radome': b = e['box']; frame(b[0], b[1], b[4], b[5], (60, 60, 70))
    for i, p in enumerate(teamA + teamB):
        fill(p[0] - .6, p[0] + .6, p[1] - .6, p[1] + .6, (40, 170, 255) if i < 4 else (240, 60, 100))
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
    mf = min(math.dist(p, q) for p, q in itertools.combinations(ffa, 2)); mab = min(math.dist(p, q) for p in teamA for q in teamB)
    print('min FFA spacing %.1f m; min A-B spacing %.1f m' % (mf, mab))
    if mf < 12: fails.append('FFA spacing')
    print('hazards: none (M1); spawn clearance from hazard volumes: n/a (0 volumes)')
    clr = min((min(dist_rect(p, (e['box'][0], e['box'][1], e['box'][4], e['box'][5])) for e in els if e['box'][2] < 1.9 and e['box'][3] > .45), p) for p in sp)
    print('closest spawn to any block: %.1f m at %s' % clr)
    if clr[0] < .6: fails.append('spawn inside or against a block')
    inb = all(abs(x) <= BOUNDS['maxX'] - CAP and abs(z) <= BOUNDS['maxZ'] - CAP for x, z in sp)
    print('all spawns inside the bounds minus the capsule:', inb)
    if not inb: fails.append('bounds')
    stand, seen, cell = raster()
    lv = {}
    for (c, t) in seen: k = 'ground 0.0' if t == 0 else '%.1f' % t; lv[k] = lv.get(k, 0) + 1
    tot = sum(len(t) for t in stand.values())
    print('3D raster (%.2f m cells): %d standable cells, %d reachable from A1; by top: %s' % (CELL, tot, len(seen),
          ', '.join('%s: %d' % (k, lv[k]) for k in sorted(lv, key=lambda s: float(s.split()[-1])) if lv[k] >= 20 or k.startswith('ground'))))
    unreach = [nm for nm, p in list(zip(ffa_names, ffa)) + A + B if (cell(*p), 0.0) not in seen]
    print('every spawn reachable on foot:', not unreach, ('(unreachable: %s)' % ', '.join(unreach)) if unreach else '')
    if unreach: fails.append('spawn reachability')
    for nm, x, z, y in LEVELS:
        c = cell(x, z); ok = (c, y) in seen
        cnt = sum(1 for (cc, t) in seen if t == y)
        print('   level %-16s y %.1f: %s (%d cells at that top reachable)' % (nm, y, 'reachable' if ok else 'NOT REACHABLE', cnt))
        if not ok: fails.append('level ' + nm)
    # hall interior: doors visible from each reachable interior cell (floor at eye 1.6, the bridge at 3.2 + 1.6); CQB, never a 3- or 4-door box
    worst = (0, None)
    hist = {}
    for (i, j), tops in stand.items():
        x, z = BOUNDS['minX'] + (i + .5) * CELL, BOUNDS['minZ'] + (j + .5) * CELL
        if not hall_inside(x, z): continue
        for t in tops:
            if t not in (0.0, 3.2) or ((i, j), t) not in seen: continue
            eye = (x, t + 1.6, z); vis = [k for k in DOORS if any(visible(eye, q, -9) for q in door_points(k))]
            hist[len(vis)] = hist.get(len(vis), 0) + 1
            if len(vis) > worst[0]: worst = (len(vis), (round(x, 2), t, round(z, 2), ''.join(vis)))
    print('hall interior: cells by doors in view %s; worst %d at %s' % (dict(sorted(hist.items())), worst[0], worst[1]))
    if worst[0] > 2: fails.append('hall door exposure')
    # team spawns hidden from every reachable elevated cell (module roofs, drill decks, the bridge; eye +1.6) at chest 1.2 and head 1.9
    bl = [e for e in els if e['role'] != 'cover']
    perch = [(BOUNDS['minX'] + (c[0] + .5) * CELL, t + 1.6, BOUNDS['minZ'] + (c[1] + .5) * CELL) for (c, t) in seen if t >= 2.5 and c[0] % 2 == 0 and c[1] % 2 == 0]
    peek = sorted({nm for nm, p in A + B for q in perch if any(not any(seg_box(q, (p[0], y, p[1]), e['box']) for e in bl) for y in (1.2, 1.9))})
    print('team spawns seen from %d elevated cells (roofs, decks, bridge): %s' % (len(perch), ', '.join(peek) or 'none'))
    if peek: fails.append('spawns overlooked')
    # longest open ground sightline (2 m grid of reachable floor cells outside the hall, eye 1.6, full cover only): no whole-map diagonals
    pts = [(x, z) for x in range(-31, 32, 2) for z in range(-25, 26, 2) if (cell(x, z), 0.0) in seen and not hall_inside(x, z)]
    long = max((math.dist(a, b), a, b) for a, b in itertools.combinations(pts, 2) if math.dist(a, b) > 30 and visible((a[0], 1.6, a[1]), (b[0], 1.6, b[1])))
    print('longest open ground sightline: %.1f m %s -> %s (map diagonal %.0f m; limit 40)' % (long[0], long[1], long[2], math.hypot(64, 52)))
    if long[0] > 40: fails.append('long sightline')
    # pilot blocks: every rail and every unstandable top is under a pilot column
    def covered(e):
        b = e['box']; cx, cz = (b[0] + b[1]) / 2, (b[4] + b[5]) / 2
        return any(abs(cx - p['center']['x']) <= p['size']['x'] / 2 + 1e-6 and abs(cz - p['center']['z']) <= p['size']['z'] / 2 + 1e-6 and p['bottom'] <= b[3] for p in PILOT)
    def roofed(e):                                     # no 1.8 m headroom above its top (under a roof or deck): nothing to plan onto
        b = e['box']; cx, cz = (b[0] + b[1]) / 2, (b[4] + b[5]) / 2
        return any(o is not e and o['box'][0] < cx < o['box'][1] and o['box'][4] < cz < o['box'][5] and b[3] - 1e-6 <= o['box'][2] < b[3] + 1.8 for o in els)
    need = [e for e in els if e['ch'] in 'r#HRGhkTMVO' and not (e['ch'] == 'V' and e['box'][3] < 2.5) and not roofed(e)]
    miss = [e['id'] for e in need if not covered(e)]
    print('pilot blocks: %d (audited %d rails and exposed unstandable tops); uncovered: %s' % (len(PILOT), len(need), miss or 'none'))
    if miss: fails.append('pilot blocks')
    boxes = {(e['id'], tuple(round(c, 3) + 0.0 for c in e['box'])) for e in els}
    def partner(e): b = e['box']; return (mirror_id(e['id']), tuple(round(c, 3) + 0.0 for c in (-b[1], -b[0], b[2], b[3], -b[5], -b[4])))
    asym = [e['id'] for e in els if partner(e) not in boxes]
    print('point symmetry: every block has its (-x,-z) partner with the mirrored id:', not asym, asym[:5] if asym else '')
    if asym: fails.append('symmetry')
    ids = [e['id'] for e in els]; dup = sorted({i for i in ids if ids.count(i) > 1})
    if dup: fails.append('duplicate ids'); print('duplicate ids:', dup)
    print('blocks: %d (+ floor; target <= 350); hazards: %d; pilot blocks: %d; spawns: %d ffa + %d + %d team' % (len(els), len(HAZ), len(PILOT), len(ffa), len(teamA), len(teamB)))
    if len(els) > 350: fails.append('block budget')
    mid = [e['id'] for e in els if 1.3 < e['box'][3] < 2.2 and e['box'][2] < .5 and e['ch'] != '/']
    print('tops between 1.3 and 2.2 m (head-glitch band; stair steps excepted):', mid or 'none')
    if mid: fails.append('cover heights')
    print('RESULT:', 'all checks pass' if not fails else 'FAILED: ' + ', '.join(fails))
    if '--json' in sys.argv:
        with open(sys.argv[sys.argv.index('--json') + 1], 'w') as f: json.dump(unity_json(), f, separators=(',', ':'))
    if '--out' in sys.argv:
        out = sys.argv[sys.argv.index('--out') + 1]; os.makedirs(out, exist_ok=True)
        with open(os.path.join(out, 'polar_m1.json'), 'w') as f: json.dump(unity_json(), f, separators=(',', ':'))
        png(os.path.join(out, 'polar_m1.png')); print('wrote', os.path.join(out, 'polar_m1.json'), 'and polar_m1.png')
    sys.exit(1 if fails else 0)
