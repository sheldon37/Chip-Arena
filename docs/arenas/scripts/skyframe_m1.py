# Skyframe M1 (floor 31 of a half-built steel tower, 56x48, levels 0 / 3.6 / 7.2, the tower-crane beam drop) draft painter and checks.
# Coordinates: x east, z north, y up, metres; Unity frame. Point symmetric: (x,z)->(-x,-z). Team A west (hoist core A), team B east.
# usage: python3 docs/arenas/scripts/skyframe_m1.py [--out DIR] [--json PATH]   (stdout: plans + checks, exit 1 on any failed check;
#        --json writes the draft only; --out writes skyframe_m1.json, draft map data in the UnityMaps.json shape, and skyframe_m1.png,
#        three top-down plans (level 0, 3.6, 7.2), 8 px per metre)
# A draft for Astra (docs/arenas/NEW-STAGES.md), NOT wired into the game. Ids carry the prefixes the presentation keys its looks and
# kit dressing on (skyframe-deck-*, -bridge-*, -rail-*, -stair-*, -column-*, -core-*, -cabin-*, -pallet-*, -bundle-*, -blocks-*, ...).
# M1 has no hinge or drop groups (NEW-STAGES.md 3); the frame, cores and decks are indestructible.
import math, json, itertools, sys, os, zlib, struct
X0, X1, Z0, Z1 = -28, 28, -26, 26           # slab (and decks) end at the open edges x +-28; z +-24 inside the party walls
# The sim clamps pilots to the bounds minus 0.42 m (NativeUnityMaps.cs), so the bounds run 4 m past the open edges: a pilot who
# walks or is knocked through a hoist landing falls into the void and dies at killY. Nothing standable exists beyond x +-28.
BOUNDS = {'minX': -32, 'maxX': 32, 'minZ': -24, 'maxZ': 24, 'ceiling': 24, 'killY': -6}
L1, L2 = 3.6, 7.2                           # deck level and collar / perch level (tops)
els = []   # dict: id, mat, role, box (x0,x1,y0,y1,z0,z1), ch, hp, plate (a walkable floor plate: decks, bridges, collar, beams)
SWAP = {'n': 's', 's': 'n', 'w': 'e', 'e': 'w', 'a': 'b', 'b': 'a'}
def mirror_id(i): return '-'.join(SWAP.get(t, t) for t in i.split('-'))
def add(i, x0, x1, y0, y1, z0, z1, ch, mat='stone', role='static', hp=0, mirror=True, plate=False):
    els.append(dict(id=i, mat=mat, role=role, box=(x0, x1, y0, y1, z0, z1), ch=ch, hp=hp, plate=plate))
    if mirror:
        m = mirror_id(i); assert m != i, 'mirror of %s keeps its id' % i
        els.append(dict(id=m, mat=mat, role=role, box=(-x1, -x0, y0, y1, -z1, -z0), ch=ch, hp=hp, plate=plate))
def stair(i, xa, xb, z0, z1, ya, yb, steps, mirror=True, axis='x', solid=None):
    # a flight of steps from (xa, ya) to (xb, yb); along x (z0..z1 wide) or along z (then xa/xb are z, z0/z1 are x).
    # ground flights are solid; elevated flights are 20 cm slabs unless solid=True (a clad scaffold stair tower)
    run = (xb - xa) / steps; rise = (yb - ya) / steps; solid = (ya == 0) if solid is None else solid
    for k in range(steps):
        a, b = sorted((xa + run * k, xa + run * (k + 1)))
        y0 = ya if solid else ya + rise * k - .2
        y1 = ya + rise * (k + 1)
        if axis == 'x': add('%s-%02d' % (i, k), a, b, max(0, y0), y1, z0, z1, '/', mat='metal', mirror=mirror)
        else: add('%s-%02d' % (i, k), z0, z1, max(0, y0), y1, a, b, '/', mat='metal', mirror=mirror)
def plates(i, x0, x1, z0, z1, holes, y0, y1, ch='d', mat='metal'):
    # a deck plate x0..x1, z0..z1 minus rectangular holes (hx0, hx1, hz0, hz1): cut into strips along x or along z (merged where
    # equal), whichever needs fewer plates
    def cut(a0, a1, b0, b1, hs):
        cs = sorted({a0, a1} | {min(max(v, a0), a1) for h in hs for v in h[:2]}); strips = []
        for a, b in zip(cs, cs[1:]):
            if b - a < 1e-6: continue
            segs, z = [], b0
            for c0, c1 in sorted((h[2], h[3]) for h in hs if h[0] < b - 1e-6 and h[1] > a + 1e-6):
                if c0 > z + 1e-6: segs.append((z, min(c0, b1)))
                z = max(z, c1)
            if z < b1 - 1e-6: segs.append((z, b1))
            if strips and strips[-1][2] == segs and abs(strips[-1][1] - a) < 1e-6: strips[-1] = (strips[-1][0], b, segs)
            else: strips.append((a, b, segs))
        return [(a, b, za, zb) for a, b, segs in strips for za, zb in segs]
    px = cut(x0, x1, z0, z1, holes); pz = [(a, b, c, d) for c, d, a, b in cut(z0, z1, x0, x1, [(h[2], h[3], h[0], h[1]) for h in holes])]
    for k, (a, b, za, zb) in enumerate(min(px, pz, key=len)): add('%s-%d' % (i, k), a, b, y0, y1, za, zb, ch, mat=mat, plate=True)
def rail_z(i, x0, x1, y0, z0, z1, h=1.1, ch='-', **k):           # a guard rail along z (x0..x1 thick)
    add(i, x0, x1, y0, y0 + h, z0, z1, ch, mat='metal', **k)
def rail_x(i, x0, x1, y0, z0, z1, h=1.1, ch='-', **k):           # a guard rail along x (z0..z1 thick)
    add(i, x0, x1, y0, y0 + h, z0, z1, ch, mat='metal', **k)

# ---- the slab (floor 31's poured floor, all of the play area; it stops at the open edges x +-28) and the brick party walls (12 m)
FLOOR = dict(id='skyframe-slab', mat='foundation', role='static', box=(-28, 28, -.5, 0, -26, 26), ch='.', hp=0, plate=True)
add('skyframe-wall-n', -28, 28, 0, 12, 24, 26, '#', mat='brick')
# ---- open edges at x +-28: 1.1 m guard rails (pilot blocks over them) at level 0 except the two material-hoist landings each
#      side (3 m, open drops into the void beyond the slab = kills at killY)
LANDINGS = [(9.5, 12.5), (-12.5, -9.5)]                          # z spans of the west landings (east: the point mirror)
for k, (a, b) in enumerate(((-24, -12.5), (-9.5, 9.5), (12.5, 24))): rail_z('skyframe-rail-edge-w-%d' % k, -28, -27.8, 0, a, b)
rail_z('skyframe-rail-deck-edge-w', -28, -27.8, L1, -24, 24)    # the 3.6 deck's outer edge is railed full length
# ---- hoist core A (blockwork 6 x 6 m, 0..9.6, its stair to the deck inside). Doors 1.8 x 2.4 at level 0: west (to the spawn yard)
#      and at the west ends of the north and south walls, so nothing inside sees down the lobbies; an upper door south at 3.6.
CX0, CX1, CZ0, CZ1, CH, DX = -25, -19, -3, 3, 9.6, -22.9
add('skyframe-core-a-wall-e', CX1 - .3, CX1, 0, CH, CZ0, CZ1, 'B')
add('skyframe-core-a-wall-w-0', CX0, CX0 + .3, 0, CH, CZ0, -.9, 'B'); add('skyframe-core-a-wall-w-1', CX0, CX0 + .3, 0, CH, .9, CZ1, 'B')
add('skyframe-core-a-wall-w-head', CX0, CX0 + .3, 2.4, CH, -.9, .9, 'B')
add('skyframe-core-a-wall-n-head', CX0 + .3, DX, 2.4, CH, CZ1 - .3, CZ1, 'B'); add('skyframe-core-a-wall-n-0', DX, CX1 - .3, 0, CH, CZ1 - .3, CZ1, 'B')
add('skyframe-core-a-wall-s-head', CX0 + .3, DX, 2.4, CH, CZ0, CZ0 + .3, 'B'); add('skyframe-core-a-wall-s-0', DX, -20.9, 0, CH, CZ0, CZ0 + .3, 'B')
add('skyframe-core-a-wall-s-1', -20.9, CX1 - .3, 6.0, CH, CZ0, CZ0 + .3, 'B')
add('skyframe-core-a-wall-s-sill', -20.9, CX1 - .3, 0, L1, CZ0, CZ0 + .3, 'B', plate=True)   # the upper door's sill, flush with the deck
stair('skyframe-stair-core-a', CZ1 - .3, CZ0 + .3, -20.9, CX1 - .3, 0, L1, 18, axis='z')      # route 1 of 3 to the deck: inside the core
# ---- deck plates (0.2 m, tops at 3.6) over the west half x -28..-7.8; holes: the core shaft, the north stairwell bay
HOLE_N = (-25.2, -20.4, 8.4, 15.6)                                          # a bay not yet decked, the north stairwell
plates('skyframe-deck-w', -28, -7.8, -24, 24, [(CX0, CX1, CZ0, CZ1), HOLE_N], L1 - .2, L1)
stair('skyframe-stair-hole-w', -20.0, -25.4, 8.4, 10.0, 0, L1, 18)                           # route 2: up the north stairwell
stair('skyframe-stair-atrium-w', -2.4, -7.8, -10.3, -8.7, 0, L1, 18)                         # route 3: from the crane well
# atrium-edge rails of the deck (falls 3.6 m into the crane well), open at the bridges, the atrium stair and the perch tower
for k, (a, b) in enumerate(((-24, -13.2), (-8.3, -5.4), (-.6, 7.8), (13.2, 24))): rail_z('skyframe-rail-atrium-w-%d' % k, -7.8, -7.6, L1, a, b)
add('skyframe-screen-w-0', -7.8, -7.6, L1, L1 + 2.4, 8.2, 10.8, 'S', mat='metal')   # sheeting on the edge beside the north bridge
# ---- the beam bridges over the crane well (2.4 m plates, 1.6+ m clear between 1.1 m rails)
add('skyframe-bridge-n', -7.8, 7.8, L1 - .2, L1, 10.8, 13.2, 'b', mat='metal', plate=True)
rail_x('skyframe-rail-bridge-n-n', -7.8, 7.8, L1, 13.0, 13.2); rail_x('skyframe-rail-bridge-n-s', -7.8, 7.8, L1, 10.8, 11.0)
# ---- scaffold perch W (7.2) at the well edge: a clad scaffold tower on the deck, its stair from the deck, a beam to the collar
PX0, PX1, PZ0, PZ1 = -11.8, -7.8, -5.0, -1.0
add('skyframe-perch-w', PX0, PX1, L1, L2, PZ0, PZ1, 'P', mat='metal', plate=True)
stair('skyframe-stair-perch-w', -17.2, PX0, -2.8, -1.2, L1, L2, 18, solid=True)            # the route to 7.2
rail_x('skyframe-rail-perch-w-n', PX0, PX1, L2, PZ1 - .2, PZ1); rail_x('skyframe-rail-perch-w-s', PX0, PX1, L2, PZ0, PZ0 + .2)
rail_z('skyframe-rail-perch-w-w', PX0, PX0 + .2, L2, PZ0 + .2, -2.8); rail_z('skyframe-rail-perch-w-e', PX1 - .2, PX1, L2, PZ0 + .2, -3.6)
add('skyframe-beam-w', PX1, -3.6, L2 - .2, L2, -3.6, -1.6, 'q', mat='metal', plate=True)
rail_x('skyframe-rail-beam-w-s', PX1, -3.6, L2, -3.6, -3.4); rail_x('skyframe-rail-beam-w-n', PX1, -3.6, L2, -1.8, -1.6)
# ---- the tower crane: mast 2 x 2 (0..24), the climbing frame at the collar, the collar ring (7.2) with its rails
add('skyframe-crane-mast', -1, 1, 0, 24, -1, 1, 'M', mat='metal', mirror=False)
add('skyframe-crane-frame', -1.6, 1.6, 6.4, 10.8, -1.6, 1.6, 'M', mat='metal', mirror=False)
add('skyframe-collar-n', -3.6, 3.6, L2 - .2, L2, 1.6, 3.6, 'o', mat='metal', plate=True)
add('skyframe-collar-w', -3.6, -1.6, L2 - .2, L2, -1.6, 1.6, 'o', mat='metal', plate=True)
rail_x('skyframe-rail-collar-n', -3.6, 3.6, L2, 3.4, 3.6); rail_z('skyframe-rail-collar-w', -3.6, -3.4, L2, -1.6, 3.4)
# ---- steel columns (0.4 m, 0..8.4, the next tier's splice stubs) on the 6 m x 8 m grid; none in the core, under the perch
for x, z in ((-8, 8), (-8, 16), (-8, -8), (-8, -16), (-14, 0), (-14, 8), (-14, 16), (-14, -8), (-14, -16), (-20, 8), (-20, 16), (-20, -8), (-20, -16)):
    add('skyframe-column-w-%d-%d' % (-x, z + 24), x - .2, x + .2, 0, 8.4, z - .2, z + .2, 'C', mat='metal')

# ---- kit footprints: site cabin / container 6.0 x 2.4 x 2.6 (full), pallet of blocks 1.2 x 1.0 x 1.1, beam bundle 6.0 x 0.9 x 1.0,
#      rebar bundle 4.0 x 1.2 x 1.0, block stack 1.6 x 1.6 x 2.4 (concrete blocks 1.6 x 0.8 x 0.8, three high), formwork 2.4 x 1.2 x 1.1
def cabin(i, x, z, along='x', y=0.0):                                  # centred at (x, z); 'K' (site cabins and site containers)
    hx, hz = (3.0, 1.2) if along == 'x' else (1.2, 3.0); add(i, x - hx, x + hx, y, y + 2.6, z - hz, z + hz, 'K', mat='metal')
def pallet(i, x, z, n=1, along='x', y=0.0):                             # n pallets of blocks side by side (breakable, heavy)
    hx, hz = (.6 * n, .5) if along == 'x' else (.5, .6 * n); add(i, x - hx, x + hx, y, y + 1.1, z - hz, z + hz, 'x', role='cover', hp=150)
def bundle(i, x, z, along='x', y=0.0, length=6.0):                     # steel beams / rebar, half cover
    hx, hz = (length / 2, .45) if along == 'x' else (.45, length / 2); add(i, x - hx, x + hx, y, y + 1.0, z - hz, z + hz, 'r', mat='metal')
def blocks(i, x, z, y=0.0, w=1.6, d=1.6):                               # a concrete block stack, full cover
    add(i, x - w / 2, x + w / 2, y, y + 2.4, z - d / 2, z + d / 2, 'k')
def formwork(i, x, z, along='x', y=0.0):                                # plywood formwork stack (breakable)
    hx, hz = (1.2, .6) if along == 'x' else (.6, 1.2); add(i, x - hx, x + hx, y, y + 1.1, z - hz, z + hz, 'w', mat='wood', role='cover', hp=34)
# ---- level 0 lobby (under the decks, 3.4 m clear). Blockwork infill between the x -14 columns (up to the deck) makes rooms west of
#      it (the NW room with the stairwell, the core yard, the SW room) and a colonnade along the well; ways through: the north
#      end (z 18..24), the yard door (z -8.2..-5.8), the south end (z -24..-18)
add('skyframe-infill-w-0', -14.1, -13.9, 0, L1 - .2, -5.8, 18.0, 'I')
add('skyframe-infill-w-2', -14.1, -13.9, 0, L1 - .2, -18.0, -8.2, 'I')
add('skyframe-infill-w-3', -13.8, -8.2, 0, L1 - .2, 7.9, 8.1, 'I')            # across the colonnade (walk round it through the well)
add('skyframe-infill-w-5', -13.8, -8.2, 0, L1 - .2, -16.1, -15.9, 'I')
add('skyframe-infill-w-4', -19.8, -14.2, 0, L1 - .2, -8.1, -7.9, 'I')           # the core yard's south side
cabin('skyframe-cabin-w-0', -22.4, 20.4)                                 # NW room site office
cabin('skyframe-cabin-w-1', -22.4, -14.4)                                # SW room: screens the core's south door from f
cabin('skyframe-cabin-w-2', -19.6, -21.0, along='z')                     # SW room: f's east screen, against the party wall
pallet('skyframe-pallet-w-0', -24.0, 14.0, 2, along='z')                 # staged at the hoist landings; the stacks break the edge walk
blocks('skyframe-blocks-w-8', -26.6, 17.0, w=2.4)
blocks('skyframe-blocks-w-9', -26.6, -8.2, w=2.4)
bundle('skyframe-bundle-w-0', -16.0, 0.0, along='z')                     # the core yard
formwork('skyframe-formwork-w-0', -17.2, -6.4)
blocks('skyframe-blocks-w-2', -17.0, 13.0)
blocks('skyframe-blocks-w-0', -11.0, 1.3)                                # colonnade
blocks('skyframe-blocks-w-1', -9.6, -4.4)
blocks('skyframe-blocks-w-4', -10.6, -12.6)
blocks('skyframe-blocks-w-3', -11.4, 19.8)
pallet('skyframe-pallet-w-4', -11.0, 11.8, 2, along='z')
# ---- level 0 crane well (open to the sky): cabins break its north-south line, low cover by the drop zones
cabin('skyframe-cabin-n', -3.6, 18.2)
cabin('skyframe-cabin-n-1', 3.4, 22.8)                                    # against the party wall
bundle('skyframe-bundle-n', -4.2, 4.9, length=4.0)
blocks('skyframe-blocks-n', -3.0, 14.8)                                   # laydown under and beside the bridges
blocks('skyframe-blocks-well-w-1', -6.0, 11.0)
blocks('skyframe-blocks-well-w-2', -5.0, -14.0)
blocks('skyframe-blocks-well-w-0', -5.0, 0.0)                              # staged at the mast's foot
pallet('skyframe-pallet-n', 4.6, 5.2, 2)
# ---- deck 3.6 (site containers craned up, block stacks, pallets, bundles). Containers screen the well edge except at the
#      bridges, the perch and the stair heads, so the decks only see each other across the bridges and the well's middle.
cabin('skyframe-container-w-0', -21.4, 20.2, y=L1)                        # the NW pocket (spawn g) along the party wall, open west and east
cabin('skyframe-container-w-3', -9.0, 21.0, along='z', y=L1)             # well-edge screens: the NE corner (with a stack),
blocks('skyframe-blocks-w-7', -9.0, 16.8, y=L1, d=2.4)
cabin('skyframe-container-w-5', -9.0, 2.0, along='z', y=L1)              # against the perch's north side,
blocks('skyframe-blocks-w-5', -9.0, 6.2, y=L1, d=2.4)
blocks('skyframe-blocks-w-6', -9.0, -6.2, y=L1, d=2.4)                   # between the atrium stair's head and the perch
cabin('skyframe-container-w-6', -9.4, -16.8, along='z', y=L1)            # south of the south bridge (0.7 m off its drop zone)
cabin('skyframe-container-w-7', -12.4, 12.0, along='z', y=L1)           # caps the north bridge's axis: a turn off the bridge
cabin('skyframe-container-w-8', -19.8, -11.6, along='z', y=L1)           # caps the south bridge's axis beyond its drop zone
bundle('skyframe-bundle-w-1', -23.0, -6.0, y=L1, length=4.0)             # the core's upper door
cabin('skyframe-container-w-2', -26.2, -13.0, along='z', y=L1)           # SW deck, across the edge strip
cabin('skyframe-container-w-4', -16.6, -19.4, y=L1)                       # SW deck
pallet('skyframe-pallet-w-3', -15.0, -8.6, 2, y=L1)                       # by the south bridgehead's drop zone
formwork('skyframe-formwork-w-1', -22.0, -21.6, y=L1)
blocks('skyframe-blocks-w-10', -18.0, 15.0, y=L1)                        # the north deck lane
blocks('skyframe-blocks-w-11', -14.0, 19.0, y=L1)
blocks('skyframe-blocks-w-12', -18.0, 7.0, y=L1)                         # north of the core

# ---- hazard: the tower-crane beam drop (UnityMaps.json `hazards[]`, the factory crane kind). Timings in s after the arm time.
def v(x, y, z): return {'x': x, 'y': y, 'z': z}
def hz(**k):
    d = dict(id='', kind='', a=v(0, 0, 0), b=v(0, 0, 0), axis=v(0, 0, 0), size=v(0, 0, 0), radius=0, start=0, period=0, offset=0,
             warn=0, rise=0, travel=0, dwell=0, hoist=0, carry=0, damage=0, tick=0, push=0, lethal=False, alternate=False, zones=[])
    d.update(k); return d
LOAD = (6.0, .9, 1.2)                                                    # the steel beam bundle (x along the jib, y, z)
ZONES = [(-13, L1, -12), (13, L1, 12), (-3, 0, 8), (3, 0, -8)]           # visit order: A deck, B deck, A well, B well
# NOTE for Astra: zone centre y is the floor the load lands on: 3.6 for the two deck zones (the factory sim lands every load at y 0
# and tests LoadY against size.y from 0; for Skyframe the fall, the landed collider and the lethal band are relative to centre.y).
# a/b run along the jib at 16 m and park the trolley over the mast; the sim may move the hook linearly between zones, the
# presentation slews the jib instead. carry 11.5 clears every top under the load's path (highest: the climbing frame, 10.8).
# Every drop shaft is open from its zone to the carried load (checked below); zones keep 0.5 m from blocks.
crane = hz(id='skyframe-crane', kind='crane', a=v(-24, 16, 0), b=v(24, 16, 0), size=v(*LOAD), carry=11.5,
           start=6, period=20, warn=3.0, travel=6.0, rise=.95, dwell=6.0, hoist=2.0, lethal=True,
           zones=[{'center': v(x, y, z), 'size': v(round(LOAD[0] + .6, 3), 2.2, round(LOAD[2] + .6, 3))} for x, y, z in ZONES])
HAZ = [crane]
PAINT_ZONE = (LOAD[0] + 1.0, LOAD[2] + 1.0)                              # painted zone footprint (the load plus 0.5 m)
REGRIP = 1.5                                                             # NativeFactoryHazards.RegripSeconds

# ---- spawns (x, z, floor); yaw from +z toward +x, facing the origin as today
teamA = [(-22.0, 1.2, 0), (-22.2, -1.4, 0), (-26.4, 1.5, 0), (-26.4, -1.5, 0)]
teamB = [(-x, -z, y) for x, z, y in teamA]
ffa_names = ['A1', 'B1', 'f', 'F', 'g', 'G', 'h', 'H']
_f, _g, _h = (-24.0, -20.4, 0), (-23.4, 22.6, L1), (-10.2, -4.0, L2)
ffa = [teamA[0], teamB[0], _f, (-_f[0], -_f[1], _f[2]), _g, (-_g[0], -_g[1], _g[2]), _h, (-_h[0], -_h[1], _h[2])]
def yaw(p): return (math.degrees(math.atan2(-p[0], -p[1])) + 360) % 360

# ---- pilot-only exclusion columns (infinite upward, NativeUnityMaps.ExcludeTops): above every rail over a drop and every
#      unstandable top (party walls, core walls, columns, the crane, edge sheeting, containers and block stacks open to the sky;
#      none over anything under a deck, where the column would cut the deck). Derived from the blocks so none is missed.
def pilot_for(e):
    x0, x1, y0, y1, z0, z1 = e['box']; return dict(id=e['id'] + '-top', center=v(round((x0 + x1) / 2, 3), 0, round((z0 + z1) / 2, 3)), size=v(round(x1 - x0, 3), 0, round(z1 - z0, 3)), bottom=round(y1 - .1, 3))
def open_sky(e):                                                          # nothing walkable above it (cabins under a deck get none)
    b = e['box']; return not any(o['plate'] and o['box'][2] >= b[3] and o['box'][0] < b[1] and o['box'][1] > b[0] and o['box'][4] < b[5] and o['box'][5] > b[4] for o in els)
def needs_pilot(e): return e['ch'] in '-#CMS' or (e['ch'] == 'B' and not e['plate']) or (e['ch'] in 'Kk' and open_sky(e))
PILOT = [pilot_for(e) for e in els if needs_pilot(e)]

# ---- level bookkeeping: a block's base is the walkable level it stands on (0, 3.6, 7.2)
def base_of(e): y0 = e['box'][2]; return L2 if y0 >= L2 - .25 else L1 if y0 >= L1 - .25 else 0.0
def hazard_rects():
    """2D footprints of the painted drop zones (x0, x1, z0, z1, name, floor): what spawns keep 3 m from."""
    return [(zn['center']['x'] - PAINT_ZONE[0] / 2, zn['center']['x'] + PAINT_ZONE[0] / 2, zn['center']['z'] - PAINT_ZONE[1] / 2, zn['center']['z'] + PAINT_ZONE[1] / 2,
             'crane-zone-%d' % j, zn['center']['y']) for j, zn in enumerate(crane['zones'])]
LEVELS = [(0.0, 'level 0 (slab)'), (L1, 'level 3.6 (decks, bridges)'), (L2, 'level 7.2 (collar, perches)')]
def in_slice(e, h):                                                       # drawn on the plan of level h: it rises through h..h+2
    y0, y1 = e['box'][2], e['box'][3]; return y0 < h + 2.0 and y1 > h + .05
def floor_at(e, h): return abs(e['box'][3] - h) < .05 and (e['plate'] or e['ch'] == '/')
def thin(e): return e['plate'] and e['box'][3] - e['box'][2] < .3        # floor plates are drawn as floor, not as blocks

def paint():
    W = X1 - X0; R = (BOUNDS['maxZ'] - BOUNDS['minZ']) // 2; out = []
    for h, name in LEVELS:
        g = [[('.' if h == 0 else ' ')] * W for _ in range(R)]
        def put(x, z, ch):
            c = int(math.floor(x - X0)); r = int((BOUNDS['maxZ'] - z) // 2)
            if 0 <= c < W and 0 <= r < R: g[r][c] = ch
        def rect(x0, x1, z0, z1, ch):
            zz = z0
            while zz < z1 - 1e-9:
                xx = x0
                while xx < x1 - 1e-9: put(xx + .25, zz + .25, ch); xx += .5
                zz += .5
        for e in els:
            if floor_at(e, h) and e['ch'] != '/': b = e['box']; rect(b[0], b[1], b[4], b[5], '.')
            if h == 0 and floor_at(e, L1) and e['ch'] != '/': b = e['box']; rect(b[0], b[1], b[4], b[5], ',')   # under a deck
        for x0, x1, z0, z1, nm, fy in hazard_rects():
            if abs(fy - h) < .05: rect(x0, x1, z0, z1, 'Z')
        for e in sorted(els, key=lambda e: e['ch'] in '#BCM-'):
            if in_slice(e, h) and not thin(e): b = e['box']; rect(b[0], b[1], b[4], b[5], e['ch'])
        for nm, (x, z, y) in [('1234'[i], p) for i, p in enumerate(teamA)] + [('5678'[i], p) for i, p in enumerate(teamB)] + list(zip(ffa_names[2:], ffa[2:])):
            if abs(y - h) < .05: put(x, z, nm)
        out.append('%s   x=-28%sx=0%sx=+27' % (name, ' ' * 24, ' ' * 24))
        for r in range(R): z = BOUNDS['maxZ'] - 2 * r; out.append(f'{z-2:+4d}..{z:+3d} ' + ''.join(g[r]))
    return '\n'.join(out)

# ---- spawn sightlines in 3D: a box blocks where the segment passes through it in x, y and z. Boxes topping out below 2.0 m above
#      the lower eye's floor never count, nor do boxes less than 2.0 m tall over their own level (half cover hides no standing pilot),
#      except floor plates (decks, bridges, the collar), which hide what is under them.
def seg_hits(a, b, e, lo):
    x0, x1, y0, y1, z0, z1 = e['box']
    if y1 < lo + 2.0 or (not e['plate'] and y1 < base_of(e) + 2.0): return False
    t0, t1 = 0.0, 1.0
    for o, d, l, hgh in ((a[0], b[0] - a[0], x0, x1), (a[1], b[1] - a[1], y0, y1), (a[2], b[2] - a[2], z0, z1)):
        if abs(d) < 1e-9:
            if not (l < o < hgh): return False
        else:
            ta, tb = (l - o) / d, (hgh - o) / d
            if ta > tb: ta, tb = tb, ta
            t0, t1 = max(t0, ta), min(t1, tb)
            if t0 >= t1: return False
    return True
def check(pairs, label):
    bad = []
    for (na, a), (nb, b) in pairs:
        lo = min(a[2], b[2]); bx = (min(a[0], b[0]) - 1, max(a[0], b[0]) + 1, min(a[1], b[1]) - 1, max(a[1], b[1]) + 1)
        near = [e for e in els if e['box'][0] < bx[1] and e['box'][1] > bx[0] and e['box'][4] < bx[3] and e['box'][5] > bx[2]]
        opens = []
        for dx, dz, ex, ez in itertools.product((-1, 0, 1), repeat=4):
            for eye in (1.2, 1.6, 1.9):
                p, q = (a[0] + dx, a[2] + eye, a[1] + dz), (b[0] + ex, b[2] + eye, b[1] + ez)
                if not any(seg_hits(p, q, e, lo) for e in near): opens.append((p, q))
        if opens:
            p, q = opens[len(opens) // 2]
            bad.append(f'{na}-{nb} ({len(opens)}/243 variants open, {math.dist(a[:2], b[:2]):.1f} m; e.g. ({p[0]:.1f},{p[1]:.1f},{p[2]:.1f})->({q[0]:.1f},{q[1]:.1f},{q[2]:.1f}))')
    print(f'{label}: {len(pairs)} pairs, {len(pairs) - len(bad)} fully blocked under +-1 m jitter x 3 eye heights (3D, per-spawn floors)')
    for s in bad: print('   OPEN', s)
    return len(bad)
def dist_rect(p, r): dx = max(r[0] - p[0], 0, p[0] - r[1]); dz = max(r[2] - p[1], 0, p[1] - r[3]); return math.hypot(dx, dz)

# ---- reachability: a 0.25 m raster of standable tops. A top is standable with 1.8 m headroom over the cell centre, no block in the
#      0.42 m capsule ring between top+0.45 and top+1.8, and no pilot exclusion column over the body; neighbours join when |dy| <= 0.25.
CELL, CAP = .25, .42
def raster():
    nx, nz = int((BOUNDS['maxX'] - BOUNDS['minX']) / CELL), int((BOUNDS['maxZ'] - BOUNDS['minZ']) / CELL)
    allb = [FLOOR] + els; buck = {}
    def cells(x0, x1, z0, z1):
        for i in range(max(0, int((x0 - BOUNDS['minX']) / CELL)), min(nx, int((x1 - BOUNDS['minX']) / CELL) + 1)):
            for j in range(max(0, int((z0 - BOUNDS['minZ']) / CELL)), min(nz, int((z1 - BOUNDS['minZ']) / CELL) + 1)): yield i, j
    for e in allb:
        b = e['box']
        for c in cells(b[0] - CAP, b[1] + CAP, b[4] - CAP, b[5] + CAP): buck.setdefault(c, []).append(e)
    pil = [(p['center']['x'] - p['size']['x'] / 2 - CAP, p['center']['x'] + p['size']['x'] / 2 + CAP, p['center']['z'] - p['size']['z'] / 2 - CAP, p['center']['z'] + p['size']['z'] / 2 + CAP, p['bottom']) for p in PILOT]
    pbuck = {}
    for p in pil:
        for c in cells(*p[:4]): pbuck.setdefault(c, []).append(p)
    def standable(cx, cz, t, local, plocal):
        for e in local:
            x0, x1, y0, y1, z0, z1 = e['box']
            if y0 < t + 1.8 and y1 > t + .01 and x0 < cx < x1 and z0 < cz < z1: return False
            if y0 < t + 1.8 and y1 > t + .45 and x0 - CAP < cx < x1 + CAP and z0 - CAP < cz < z1 + CAP: return False
        for x0, x1, z0, z1, bot in plocal:
            if t + 1.95 >= bot and x0 < cx < x1 and z0 < cz < z1: return False
        return True
    tops = {}
    for i in range(nx):
        for j in range(nz):
            cx, cz = BOUNDS['minX'] + (i + .5) * CELL, BOUNDS['minZ'] + (j + .5) * CELL; local = buck.get((i, j), []); plocal = pbuck.get((i, j), [])
            ts = sorted({round(e['box'][3], 3) for e in local if e['box'][0] < cx < e['box'][1] and e['box'][4] < cz < e['box'][5]})
            ok = [t for t in ts if standable(cx, cz, t, local, plocal)]
            if ok: tops[(i, j)] = ok
    def cell_of(x, z): return int((x - BOUNDS['minX']) / CELL), int((z - BOUNDS['minZ']) / CELL)
    i, j = cell_of(teamA[0][0], teamA[0][1]); start = (i, j, round(teamA[0][2], 3))
    seen = {start} if start[2] in tops.get((i, j), []) else set(); todo = list(seen)
    while todo:
        i, j, t = todo.pop()
        for di, dj in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            for t2 in tops.get((i + di, j + dj), ()):
                n = (i + di, j + dj, t2)
                if abs(t2 - t) <= .25 + 1e-6 and n not in seen: seen.add(n); todo.append(n)
    return tops, seen, cell_of, standable, buck, pbuck

def unity_json():
    def block(e):
        x0, x1, y0, y1, z0, z1 = e['box']
        return {'id': e['id'], 'material': e['mat'], 'pos': v(round((x0 + x1) / 2, 3), round((y0 + y1) / 2, 3), round((z0 + z1) / 2, 3)),
                'size': v(round(x1 - x0, 3), round(y1 - y0, 3), round(z1 - z0, 3)), 'rotation': {'x': 0, 'y': 0, 'z': 0, 'w': 1}, 'role': e['role'], 'mass': 26, 'hp': e['hp']}
    sp = lambda p: {'pos': v(p[0], round(p[2] + 1.1, 3), p[1]), 'yaw': round(yaw(p), 3)}
    return {'schema': 1, 'materials': [],
            'maps': [{'id': 'skyframe', 'name': 'Skyframe', 'tag': 'Floor 31. The crane does not stop for lunch.', 'theme': 'skyframe',
                      'music': 'skyframe-high-steel', 'ambience': 'skyframe-wind', 'look': 'skyframe-high-noon', 'sky': 0x8CC4F0, 'floor': 0x9E9B93, 'accent': 0xFF7A1A,
                      'rotation': False, 'bounds': BOUNDS, 'spawns': {'ffa': [sp(p) for p in ffa], 'teams': [{'side': 0, 'points': [sp(p) for p in teamA]}, {'side': 1, 'points': [sp(p) for p in teamB]}]},
                      'blocks': [block(FLOOR)] + [block(e) for e in els], 'segments': [], 'pilotBlocks': PILOT, 'hazards': HAZ, 'nav': {'nodes': [], 'links': []}}]}

def png(path, scale=8):
    PW, GAP = (X1 - X0) * scale, 2 * scale; PH = (BOUNDS['maxZ'] - BOUNDS['minZ']) * scale; W, H = 3 * PW + 2 * GAP, PH
    img = bytearray(b'\x18\x1a\x1e' * W * H)
    def fill(k, x0, x1, z0, z1, c, alpha=1.0):
        ox = k * (PW + GAP); i0, i1 = ox + int((x0 - X0) * scale), ox + int((x1 - X0) * scale); j0, j1 = int((BOUNDS['maxZ'] - z1) * scale), int((BOUNDS['maxZ'] - z0) * scale)
        for j in range(max(0, j0), min(H, j1)):
            for i in range(max(ox, i0), min(ox + PW, i1)):
                o = (j * W + i) * 3
                for q in range(3): img[o + q] = int(img[o + q] * (1 - alpha) + c[q] * alpha)
    col = {'#': (150, 84, 66), 'B': (150, 146, 136), 'I': (170, 160, 140), 'C': (70, 110, 150), 'b': (120, 128, 136), 'd': (120, 128, 136), 'q': (150, 150, 160), 'o': (150, 150, 160),
           'P': (190, 150, 60), 'M': (240, 180, 30), '-': (230, 120, 40), 'S': (60, 150, 90), '/': (200, 200, 205), 'K': (60, 110, 170), 'x': (185, 135, 80),
           'k': (160, 160, 150), 'r': (120, 90, 80), 'w': (200, 160, 100)}
    for k, (h, name) in enumerate(LEVELS):
        if h == 0: fill(k, X0, X1, BOUNDS['minZ'], BOUNDS['maxZ'], (112, 114, 116))                      # the slab, open to the sky
        else: fill(k, X0, X1, BOUNDS['minZ'], BOUNDS['maxZ'], (36, 40, 48))                                # air
        for e in els:
            if floor_at(e, h) and e['ch'] != '/': b = e['box']; fill(k, b[0], b[1], b[4], b[5], (128, 132, 136))
            if h == 0 and floor_at(e, L1) and e['ch'] != '/': b = e['box']; fill(k, b[0], b[1], b[4], b[5], (78, 80, 84))   # under a deck
        for x0, x1, z0, z1, nm, fy in hazard_rects():
            if abs(fy - h) < .05: fill(k, x0, x1, z0, z1, (240, 190, 30), .75)
        for e in sorted(els, key=lambda e: e['box'][3]):
            if in_slice(e, h) and not thin(e): b = e['box']; fill(k, b[0], b[1], b[4], b[5], col.get(e['ch'], (128, 128, 128)), .55 if e['box'][3] - max(h, base_of(e)) < 1.25 else 1.0)
        for i, p in enumerate(teamA + teamB):
            if abs(p[2] - h) < .05: fill(k, p[0] - .6, p[0] + .6, p[1] - .6, p[1] + .6, (60, 200, 255) if i < 4 else (255, 90, 120))
        for p in ffa[2:]:
            if abs(p[2] - h) < .05: fill(k, p[0] - .5, p[0] + .5, p[1] - .5, p[1] + .5, (250, 250, 250))
    raw = b''.join(b'\x00' + bytes(img[j * W * 3:(j + 1) * W * 3]) for j in range(H))
    ch = lambda t, d: struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    with open(path, 'wb') as f: f.write(b'\x89PNG\r\n\x1a\n' + ch(b'IHDR', struct.pack('>IIBBBBB', W, H, 8, 2, 0, 0, 0)) + ch(b'IDAT', zlib.compress(raw, 9)) + ch(b'IEND', b''))

if __name__ == '__main__':
    print(paint()); fails = []
    def need(ok, what):
        if not ok: fails.append(what)
        return ok
    named = list(zip(ffa_names, ffa)); bad = check(list(itertools.combinations(named, 2)), 'FFA')
    A = [(f'A{i+1}', p) for i, p in enumerate(teamA)]; B = [(f'B{i+1}', p) for i, p in enumerate(teamB)]; bad += check([(p, q) for p in A for q in B], 'Teams A-B')
    need(bad == 0, 'spawn sightlines')
    sp = ffa + teamA[1:] + teamB[1:]
    mf = min(math.dist(p[:2], q[:2]) for p, q in itertools.combinations(ffa, 2)); mab = min(math.dist(p[:2], q[:2]) for p in teamA for q in teamB)
    print('min FFA spacing %.1f m (plan distance); min A-B spacing %.1f m' % (mf, mab)); need(mf >= 12, 'FFA spacing'); need(mab >= 24, 'A-B spacing')
    print('FFA floors: ' + ', '.join('%s %.1f' % (n, p[2]) for n, p in named))
    worst = min((dist_rect(p, r[:4]), r[4], p[:2]) for p in sp for r in hazard_rects())
    print('closest spawn to any hazard volume: %.1f m (%s, spawn %s)' % worst); need(worst[0] >= 3, 'hazard clearance')
    inb = all(X0 + CAP <= x <= X1 - CAP and BOUNDS['minZ'] + CAP <= z <= BOUNDS['maxZ'] - CAP for x, z, y in sp)
    print('all spawns on the slab and inside the bounds minus the capsule:', inb); need(inb, 'bounds')
    past = [e['id'] for e in [FLOOR] + els if e['box'][0] < X0 - 1e-6 or e['box'][1] > X1 + 1e-6]
    print('edges: slab and decks end at x %+d/%+d, bounds x %+d/%+d (4 m of void), killY %d; blocks past the slab edge: %s; hoist landings (open, falls kill): '
          'west z %s, east z %s' % (X0, X1, BOUNDS['minX'], BOUNDS['maxX'], BOUNDS['killY'], past or 'none', LANDINGS, [(-b, -a) for a, b in LANDINGS]))
    need(not past and BOUNDS['minX'] < X0 and BOUNDS['maxX'] > X1, 'open edges')
    # crane zones: 0.5 m clear of every block at the zone's level, and an open shaft from the zone up to the carried load
    zc, shaft = [], []
    for x0, x1, z0, z1, nm, fy in hazard_rects():
        for e in els:
            b = e['box']
            if b[3] <= fy + .01 or b[2] >= fy + 2.5: pass
            else: dx = max(x0 - b[1], 0, b[0] - x1); dz = max(z0 - b[5], 0, b[4] - z1); zc.append((math.hypot(dx, dz), e['id'], nm))
            if b[3] > fy + .01 and b[2] < crane['carry'] + LOAD[1] and b[0] < x1 and b[1] > x0 and b[4] < z1 and b[5] > z0: shaft.append((nm, e['id']))
    print('closest block to a painted crane zone: %.2f m (%s, %s); blocks in the drop shafts: %s' % (min(zc) + (shaft or 'none',))); need(min(zc)[0] >= .5 and not shaft, 'crane zone clearance')
    # cover heights: nothing tops out 1.3..2.2 m over its own level (head-glitch band), rails and stairs aside
    band = [e['id'] for e in els if e['ch'] not in '-/' and not e['plate'] and 1.3 < e['box'][3] - base_of(e) < 2.2]
    print('blocks topping out 1.3..2.2 m over their level:', band or 'none'); need(not band, 'cover band')
    # pilot exclusion over every rail and unstandable top
    pil_ids = {p['id'] for p in PILOT}; miss = [e['id'] for e in els if needs_pilot(e) and e['id'] + '-top' not in pil_ids]
    print('pilot blocks: %d (rails %d, edge sheeting %d, walls/cores %d, columns %d, crane %d, open-sky containers/stacks %d); rails or tops without one: %s'
          % (len(PILOT), sum(e['ch'] == '-' for e in els), sum(e['ch'] == 'S' for e in els), sum(e['ch'] in '#B' and not e['plate'] for e in els), sum(e['ch'] == 'C' for e in els),
             sum(e['ch'] == 'M' for e in els), sum(e['ch'] in 'Kk' and needs_pilot(e) for e in els), miss or 'none')); need(not miss, 'pilot blocks')
    # stairs: risers <= 0.2 m, flights >= 1.6 m wide, every flight ends flush with the level it serves
    flights = {}
    for e in els:
        if e['ch'] == '/': flights.setdefault(e['id'].rsplit('-', 1)[0], []).append(e['box'])
    bad_st = [f for f, bs in flights.items() if max(b2[3] - b1[3] for b1, b2 in zip(sorted(bs, key=lambda b: b[3]), sorted(bs, key=lambda b: b[3])[1:])) > .2 + 1e-6
              or min(min(b[1] - b[0], b[5] - b[4]) for b in bs) < .29 or min(max(b[1] - b[0], b[5] - b[4]) for b in bs) < 1.6 - 1e-6 or round(max(b[3] for b in bs), 3) not in (L1, L2)]
    print('stairs: %d flights (%s to 3.6, %s to 7.2), risers <= 0.2 m, 1.6 m wide; bad: %s' % (len(flights), sum(round(max(b[3] for b in bs), 3) == L1 for bs in flights.values()),
          sum(round(max(b[3] for b in bs), 3) == L2 for bs in flights.values()), bad_st or 'none')); need(not bad_st, 'stairs')
    tops, seen, cell_of, standable, buck, pbuck = raster()
    for h, name in LEVELS:
        tot = sum(1 for c, ts in tops.items() for t in ts if abs(t - h) < .05); r = sum(1 for c in seen if abs(c[2] - h) < .05)
        print('raster %s: %d of %d standable cells reachable from A1' % (name, r, tot)); need(r > 0, 'reach ' + name)
    void = sum(1 for i, j, t in seen if not X0 < BOUNDS['minX'] + (i + .5) * CELL < X1)
    print('reachable raster cells beyond the slab edge (the void): %d' % void); need(void == 0, 'void')
    unreached = []
    for n, p in list(zip(ffa_names, ffa)) + A + B:
        i, j = cell_of(p[0], p[1]); ok = (i, j, round(p[2], 3)) in seen and standable(p[0], p[1], p[2], buck.get((i, j), []), pbuck.get((i, j), []))
        if not ok: unreached.append(n)
    for e in els:                                                         # every deck plate, bridge, perch, beam and collar piece
        if e['plate'] and e['ch'] != 'B':
            b = e['box']; (i0, j0), (i1, j1) = cell_of(b[0], b[4]), cell_of(b[1], b[5])
            if not any((i, j, round(b[3], 3)) in seen for i in range(i0, i1 + 1) for j in range(j0, j1 + 1)): unreached.append(e['id'])
    print('every spawn and walkable plate reachable on foot from A1:', unreached or 'yes'); need(not unreached, 'reachability')
    c = crane; cyc = c['travel'] + c['warn'] + c['rise'] + c['dwell'] + REGRIP + c['hoist']; zs = [z['center'] for z in c['zones']]
    hop = max(math.hypot(a['x'] - b['x'], a['z'] - b['z']) for a, b in zip(zs, zs[1:] + zs[:1]))
    print('crane trolley/slew: longest zone-to-zone hop %.1f m in %.1f s = %.1f m/s (sprint 6.8); lethal only while the load falls through the zone box (%.2f s) and %.2f s after impact'
          % (hop, c['travel'], hop / c['travel'], c['rise'], .06))
    print('crane: %d zones (%s), every %.0f s (cycle %.2f s); %.1f s travel + %.1f s telegraph over the zone; fall %.2f s from %.1f m; landed cover %.1f s; first drop at arm + %.0f s'
          % (len(c['zones']), ', '.join('y %.1f' % z['center']['y'] for z in c['zones']), c['period'], cyc, c['travel'], c['warn'], c['rise'], c['carry'], c['dwell'], c['start'] + c['travel'] + c['warn'])); need(cyc <= c['period'] and c['warn'] >= 3, 'crane timing')
    print('blocks: %d (+ slab); hazards: %d; pilot blocks: %d; spawns: %d FFA + 2 x %d team' % (len(els), len(HAZ), len(PILOT), len(ffa), len(teamA))); need(len(els) <= 350, 'block budget')
    if '--json' in sys.argv:
        with open(sys.argv[sys.argv.index('--json') + 1], 'w') as f: json.dump(unity_json(), f, separators=(',', ':'))
    if '--out' in sys.argv:
        out = sys.argv[sys.argv.index('--out') + 1]; os.makedirs(out, exist_ok=True)
        with open(os.path.join(out, 'skyframe_m1.json'), 'w') as f: json.dump(unity_json(), f, separators=(',', ':'))
        png(os.path.join(out, 'skyframe_m1.png')); print('wrote', os.path.join(out, 'skyframe_m1.json'), 'and skyframe_m1.png')
    print('FAILED: ' + ', '.join(fails) if fails else 'all checks pass')
    sys.exit(1 if fails else 0)
