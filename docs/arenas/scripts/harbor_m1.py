# Pier 9 M1 (harbor, 80x60 night container terminal: the Quay, the Gantry Yard, the Transit Shed; gantry container drop, quay edge) draft painter
# with spawn LOS, hazard clearance, layered reachability, pilot-block, spawn-exposure and overlap checks.
# Coordinates: x east, z north, y up, metres; Unity frame. MIRROR symmetric across x = 0: (x, z) -> (-x, z); partner ids swap e/w and a/b
# (n/s stay). The quay edge is the north edge (z = +30, no wall: a 2.2 m fall into the basin); team A spawns in the west truck yard, B in the east.
# usage: python3 docs/arenas/scripts/harbor_m1.py [--out DIR] [--json PATH]   (stdout: plan + checks; --json writes the draft only; --out writes
#        harbor_m1.json, draft map data in the UnityMaps.json shape, and harbor_m1.png, a top-down plan, 8 px per metre); exit 1 if a check fails.
# A draft for Astra (docs/arenas/NEW-STAGES.md), NOT wired into the game. Ids carry the prefixes the presentation keys its looks and kit dressing
# on (harbor-stack-* containers, -reefer-*, -stair-*, -deck-*, -rail-*, -leg-*, -sill-*, -bridge-*, -gantry-*, -hatch-*, -rack-*, -crate-*,
# -cage-*, -forklift-*, -truck-*, -gatehouse-*, -office-*, -shed-*, -bollard-*, -kerb-*, -gangway-*, -wall-*); geometry, spawns and hazard timing
# become Astra's once integrated.
import math, json, itertools, sys, os, zlib, struct
X0, X1, Z0, Z1 = -42, 42, -32, 30           # slab; play x +-40, z -30..+30 (the north edge is the quay edge, the water beyond)
# The sim clamps a pilot to the bounds minus the capsule (Core/NativeUnityMaps.cs Clamp), so the bounds run 4 m past the quay
# edge: a pilot who steps or is knocked off the edge at z 30 falls into the basin (killY, and the proposed harbor-water volume).
BOUNDS = {'minX': -40, 'maxX': 40, 'minZ': -30, 'maxZ': 34, 'ceiling': 20, 'killY': -6}
CAPSULE = .42
els = []   # dict: id, mat, role, box (x0,x1,y0,y1,z0,z1), ch, hp
SWAP = {'w': 'e', 'e': 'w', 'a': 'b', 'b': 'a'}                     # mirror across x = 0: west/east and team a/b swap, north/south stay
def mirror_id(i): return '-'.join(SWAP.get(t, t) for t in i.split('-'))
def add(i, x0, x1, y0, y1, z0, z1, ch, mat='metal', role='static', hp=0, mirror=True):
    b = tuple(round(c, 4) for c in (x0, x1, y0, y1, z0, z1))
    els.append(dict(id=i, mat=mat, role=role, box=b, ch=ch, hp=hp))
    if mirror:
        m = mirror_id(i); assert m != i, 'mirror of %s keeps its id' % i
        els.append(dict(id=m, mat=mat, role=role, box=(-b[1], -b[0], b[2], b[3], b[4], b[5]), ch=ch, hp=hp))
TIER = 2.6                                                          # container grid: 2.4 wide, 2.6 a tier, 6.0 / 12.2 long
def stack(i, x0, x1, z0, z1, tiers, **k): add(i, x0, x1, 0, TIER * tiers, z0, z1, 'nNM'[tiers - 1], **k)
def stair(i, xa, xb, z0, z1, ya, yb, steps, solid=99.0, axis='x', mirror=True):
    # a flight of steps from (xa, ya) to (xb, yb); along x (z0..z1 wide) or along z (then xa/xb are z, z0/z1 are x). Steps whose top is at most
    # `solid` above the flight's base are solid down to it; higher ones are 20 cm slabs (an open steel flight you can walk under)
    run = (xb - xa) / steps; rise = (yb - ya) / steps
    for k in range(steps):
        a, b = sorted((xa + run * k, xa + run * (k + 1))); y1 = ya + rise * (k + 1)
        y0 = ya if y1 - ya <= solid + 1e-6 else y1 - .2
        if axis == 'x': add('%s-%02d' % (i, k), a, b, y0, y1, z0, z1, '/', mirror=mirror)
        else: add('%s-%02d' % (i, k), z0, z1, y0, y1, a, b, '/', mirror=mirror)

# ---- shell: terminal fence / reefer wall (W, E, 7.8 m) and the transit shed's back wall (S, 8 m), tops unstandable; NO wall on the north:
#      the slab ends at the quay edge with a 0.2 m kerb and the bollards (a real fall)
FLOOR = dict(id='harbor-floor', mat='foundation', role='static', box=(-42, 42, -.5, 0, -32, 30), ch='.', hp=0)
add('harbor-wall-s', -42, 42, 0, 8.0, -32, -30, '#', mat='brick', mirror=False)
add('harbor-wall-w', -42, -40, 0, 7.8, -30, 30, '#')
add('harbor-kerb-n', -40, 40, 0, .2, 29.7, 30, 'k', mat='stone', mirror=False)
for k, x in enumerate((-36, -28, -20, -12)): add('harbor-bollard-n-w-%d' % k, x - .4, x + .4, 0, 1.0, 28.7, 29.5, 'o')
add('harbor-gangway-c', -1.6, 1.6, 0, 2.6, 28.2, 29.7, 'g', mirror=False)          # the freighter's gangway landing at the centre of the edge

# ---- the Quay (z 13.6..30): the ship-to-shore crane's portal over the centre. Deck at 9.0 (10 x 6 m) with 1.1 m rails, four legs (full cover,
#      to 15 m), sill beams between each leg pair (1.1 m, half cover); one 45-riser flight (0.2 x 0.3 m) from each side, solid to 2.0 m, open above
add('harbor-deck-sts', -5, 5, 8.8, 9.0, 21.0, 27.0, 'D', mirror=False)
add('harbor-rail-sts-n', -5, 5, 9.0, 10.1, 26.88, 27.0, 'r', mirror=False)
add('harbor-rail-sts-s', -5, 5, 9.0, 10.1, 21.0, 21.12, 'r', mirror=False)
add('harbor-rail-sts-w-0', -5, -4.88, 9.0, 10.1, 21.12, 23.1, 'r')
add('harbor-rail-sts-w-1', -5, -4.88, 9.0, 10.1, 24.9, 26.88, 'r')
for k, (z0, z1) in enumerate(((19.8, 21.0), (27.0, 28.2))): add('harbor-leg-sts-w-%d' % k, -6.2, -5.0, 0, 15.0, z0, z1, 'L')
add('harbor-sill-sts-w', -6.2, -5.0, 0, 1.1, 21.0, 27.0, 's')
stair('harbor-stair-sts-w', -18.5, -5.0, 23.1, 24.9, 0, 9.0, 45, solid=2.0)
# quay cargo: two 20 ft boxes stacked at each end (the quay spawns' cover, high enough to hide them from the deck), hatch-cover stacks (two lids,
# 2.4 m) 2.4 m off the mouths of the yard alleys, a 20 ft box against the centre stack; lashing cages (half) on the open apron
stack('harbor-stack-q-w', -28.4, -26.0, 20.6, 26.6, 2)
add('harbor-hatch-w-0', -24.0, -18.0, 0, 2.4, 16.0, 18.4, 'H')
add('harbor-hatch-w-1', -11.6, -6.4, 0, 2.4, 16.0, 20.0, 'H')
stack('harbor-stack-q-c', -3.0, 3.0, 13.6, 16.0, 1, mirror=False)
add('harbor-cage-q-w-0', -22.4, -20.0, 0, 1.2, 25.8, 27.0, 'c', role='cover', hp=150)
add('harbor-cage-q-w-1', -34.4, -32.0, 0, 1.2, 16.4, 17.6, 'c', role='cover', hp=150)

# ---- the Gantry Yard (z -5.6..13.6): stepped container rows either side of the cross aisle (z 1.6..6.4, the five drop slots), N-S alleys 3.2 m
#      at x +-7.7; front rows one high (terraces at 2.6), then two high (tops at 5.2); three-high rows (7.8) are unstandable
AISLE = (1.6, 6.4)
stack('harbor-stack-n-w-0', -21.5, -9.3, 6.4, 8.8, 1)
stack('harbor-stack-n-w-1', -21.5, -9.3, 8.8, 11.2, 2)
stack('harbor-stack-n-w-2', -21.5, -9.3, 11.2, 13.6, 2)
stack('harbor-stack-n-c-0', -6.1, 6.1, 6.4, 8.8, 2, mirror=False)
stack('harbor-stack-n-c-1', -6.1, 6.1, 8.8, 11.2, 2, mirror=False)
stack('harbor-stack-n-c-2', -6.1, 6.1, 11.2, 13.6, 3, mirror=False)
stack('harbor-stack-s-w-0', -21.5, -9.3, -0.8, 1.6, 1)
stack('harbor-stack-s-w-1', -21.5, -9.3, -3.2, -0.8, 2)
stack('harbor-stack-s-w-2', -21.5, -9.3, -5.6, -3.2, 3)
stack('harbor-stack-s-c-0', -6.1, 6.1, -0.8, 1.6, 2, mirror=False)
stack('harbor-stack-s-c-1', -6.1, 6.1, -3.2, -0.8, 2, mirror=False)
stack('harbor-stack-s-c-2', -6.1, 6.1, -5.6, -3.2, 2, mirror=False)
# stairs at the row ends: ground -> 2.6 at the outer end of each front row; 2.6 -> 5.2 on the front row's top, against the row behind it
stair('harbor-stair-n-w-0', -25.4, -21.5, 6.6, 8.6, 0, 2.6, 13)
stair('harbor-stair-n-w-1', -13.2, -9.3, 6.8, 8.8, 2.6, 5.2, 13)
stair('harbor-stair-s-w-0', -25.4, -21.5, -0.6, 1.4, 0, 2.6, 13)
stair('harbor-stair-s-w-1', -13.2, -9.3, -0.8, 1.2, 2.6, 5.2, 13)
# gangways over the N-S alleys at 5.2 tie each side's tops to the centre stacks
add('harbor-bridge-n-w', -9.3, -6.1, 5.0, 5.2, 9.0, 11.0, '=')
add('harbor-bridge-s-w', -9.3, -6.1, 5.0, 5.2, -3.0, -1.0, '=')
# the gantry's runway (the crane hazard's rail line): girders at 16.7..17.5 along the yard's back faces, on columns against the back rows at
# x +-15.4 (clear of the alleys and lanes); the bridge, trolley, hook and load ride above and are not colliders (as the Factory's crane)
for k, (z0, z1) in enumerate(((13.6, 14.4), (-6.4, -5.6))):
    nm = 'ns'[k]
    add('harbor-gantry-runway-%s' % nm, -21.5, 21.5, 16.7, 17.5, z0, z1, 'u', mirror=False)
    add('harbor-gantry-column-%s-w' % nm, -15.8, -15.0, 0, 16.7, z0, z1, 'C')

# ---- the west truck yard (team A; the east yard is its mirror, team B): the reefer row on the quay side, the gatehouse on the fence line, a 40 ft
#      box on its chassis parked across the cross aisle's mouth (the fixed cover that ends the gantry lane's sightline and shields the spawn pocket
#      behind it from the centre tops and the deck); exits north (fence lane, reefer alley), east (round the truck) and south (road, shed end)
stack('harbor-reefer-a', -36.6, -24.4, 11.2, 13.6, 3)             # a 40 ft reefer row, three high; a 3.4 m lane along the fence behind it
add('harbor-crate-a', -34.4, -32.0, 0, 1.2, -5.2, -4.0, 'x', mat='wood', role='cover', hp=34)
add('harbor-truck-a-trailer', -30.4, -28.0, 0, 3.9, -6.8, 5.4, 'T')   # 40 ft box on its chassis
add('harbor-truck-a-cab', -30.4, -28.0, 0, 3.4, 5.4, 8.4, 'T')
add('harbor-gatehouse-a', -40, -35.2, 0, 3.2, -3.0, 3.0, 'G', mat='brick')

# ---- the road (z -10..-5.6) between the yard and the shed; the tally office plugs it at the centre
add('harbor-office-c', -3.0, 3.0, 0, 3.0, -10.0, -5.6, 'O', mat='brick', mirror=False)

# ---- the Transit Shed (z -30..-10, x -40..40): open-sided, roof at 7.0 (unstandable, it blocks LOS from above), columns on the open north side.
#      Double pallet racks (2.4 deep, 4.8 m, full) in rows across the shed every 14 m (x 0, +-14, +-28) make the side bays; the rows' gaps alternate
#      (rows 0/+-28 open north and mid-south, rows +-14 open mid-north and south) so no line runs through two rows
add('harbor-shed-roof', -40, 40, 7.0, 7.4, -30, -10, 'R', mirror=False)
for k, x in enumerate((-37.6, -18.6, -9.3)): add('harbor-shed-column-w-%d' % k, x - .2, x + .2, 0, 7.0, -10.4, -10.0, 'C')
RACK = 4.8
add('harbor-rack-c-0', -1.2, 1.2, 0, RACK, -30.0, -24.4, 'X', mirror=False)
add('harbor-rack-c-1', -1.2, 1.2, 0, RACK, -20.8, -14.4, 'X', mirror=False)
add('harbor-rack-w-14-0', -15.2, -12.8, 0, RACK, -26.8, -20.4, 'X')
add('harbor-rack-w-14-1', -15.2, -12.8, 0, RACK, -16.8, -10.0, 'X')
add('harbor-rack-w-28-0', -29.2, -26.8, 0, RACK, -30.0, -24.4, 'X')
add('harbor-rack-w-28-1', -29.2, -26.8, 0, RACK, -20.8, -10.0, 'X')
# bay contents, alternating half and full cover: crate stacks (1.2 m crates; wood, breakable), drum cages (metal, breakable), a parked forklift
add('harbor-crate-w-0', -38.8, -36.4, 0, 2.4, -16.4, -14.0, 'x', mat='wood', role='cover', hp=34)        # end bay (the yard's shed door)
add('harbor-cage-w-0', -34.0, -31.6, 0, 1.2, -22.4, -21.2, 'c', role='cover', hp=150)
add('harbor-crate-w-1', -38.8, -36.4, 0, 1.2, -27.2, -24.8, 'x', mat='wood', role='cover', hp=34)
add('harbor-crate-w-2', -26.6, -24.2, 0, 2.4, -18.4, -16.0, 'x', mat='wood', role='cover', hp=34)        # west bay
add('harbor-crate-w-3', -20.4, -18.0, 0, 1.2, -13.6, -12.4, 'x', mat='wood', role='cover', hp=34)
add('harbor-crate-w-6', -24.4, -22.0, 0, 2.4, -13.6, -11.2, 'x', mat='wood', role='cover', hp=34)
add('harbor-cage-w-1', -19.6, -17.2, 0, 1.2, -23.6, -22.4, 'c', role='cover', hp=150)
add('harbor-forklift-w', -24.4, -23.2, 0, 2.4, -28.4, -25.6, 'f')
add('harbor-crate-w-4', -9.6, -7.2, 0, 1.2, -27.2, -24.8, 'x', mat='wood', role='cover', hp=34)          # centre-west bay
add('harbor-crate-w-5', -9.0, -6.6, 0, 2.4, -21.4, -19.0, 'x', mat='wood', role='cover', hp=34)
add('harbor-cage-w-2', -5.6, -3.2, 0, 1.2, -16.0, -14.8, 'c', role='cover', hp=150)
add('harbor-cage-w-3', -11.2, -8.8, 0, 1.2, -13.2, -12.0, 'c', role='cover', hp=150)

# ---- hazards (UnityMaps.json `hazards[]`, the Factory's `crane` kind, FACTORY-SIMULATION-CONTRACT.md 2): the rail-mounted gantry carries a 20 ft
#      container (6.0 x 2.6 x 2.4, lying along x) at 12 m along the cross aisle and drops it into one of five painted slots every 18 s
def v(x, y, z): return {'x': x, 'y': y, 'z': z}
def hz(**k):
    d = dict(id='', kind='', a=v(0, 0, 0), b=v(0, 0, 0), axis=v(0, 0, 0), size=v(0, 0, 0), radius=0, start=0, period=0, offset=0,
             warn=0, rise=0, travel=0, dwell=0, hoist=0, carry=0, damage=0, tick=0, push=0, lethal=False, alternate=False, zones=[])
    d.update(k); return d
REGRIP = 1.5                                                             # NativeFactoryHazards.RegripSeconds: the landed load stays a collider
ZONE_Z = 4.0
ZONES = [-16, 8, -8, 16, 0]                                              # visit order: A side, B side, A, B, centre last (odd rounds start at 8)
LOAD = v(6.0, 2.6, 2.4)
crane = hz(id='harbor-gantry', kind='crane', a=v(-20, 17.5, ZONE_Z), b=v(20, 17.5, ZONE_Z), size=LOAD, carry=12.0,
           start=6, period=18, warn=3.0, travel=4.0, rise=1.0, dwell=8.0 - REGRIP, hoist=2.0, lethal=True,
           zones=[{'center': v(x, 0, ZONE_Z), 'size': v(LOAD['x'] + .6, 2.2, LOAD['z'] + .6)} for x in ZONES])
HAZ = [crane]
PAINT_ZONE = (LOAD['x'] + 1.0, LOAD['z'] + 1.0)                          # painted slot: the load plus 0.5 m (the kill box plus 0.2 m)
# the water: a proposed kill volume over the basin beyond the quay edge (Astra: not a hazard kind; a splash event for presentation)
KILL = [{'id': 'harbor-water', 'center': v(0, -4.5, 45.1), 'size': v(84, 7, 29.8)}]   # x -42..42, y -8..-1, z 30.2..60

# ---- spawns (yaw from +z toward +x, facing the origin as today); mirror: team B = team A with x -> -x
teamA = [(-35.5, 6.6), (-33.0, -1.0), (-37.5, -5.6), (-32.6, 3.8)]
teamB = [(-x, z) for x, z in teamA]
ffa_names = ['A1', 'B1', 'f', 'F', 'g', 'G', 'h', 'H']
ffa = [teamA[0], teamB[0], (-34.5, 24.0), (34.5, 24.0), (-20.0, -27.4), (20.0, -27.4), (-11.6, -7.8), (11.6, -7.8)]
def yaw(p): return (math.degrees(math.atan2(-p[0], -p[1])) + 360) % 360

# ---- pilot-only blocking columns (bots jump what their obstacle ray hits): above the deck rails and every unstandable top
def pb(i, x0, x1, z0, z1, bottom): return dict(id=i, center=v(round((x0 + x1) / 2, 3), 0, round((z0 + z1) / 2, 3)), size=v(round(x1 - x0, 3), 0, round(z1 - z0, 3)), bottom=bottom)
PILOT = []
def pilot_over(prefixes, drop=.1):
    for e in els:
        if any(e['id'].startswith(p) for p in prefixes):
            x0, x1, y0, y1, z0, z1 = e['box']; PILOT.append(pb(e['id'].replace('harbor-', 'harbor-top-', 1), x0, x1, z0, z1, round(y1 - drop, 3)))
pilot_over(('harbor-bollard-',), drop=.05)                                  # 1.0 m bollards at the edge: no hop over them into the basin
pilot_over(('harbor-wall-', 'harbor-rail-', 'harbor-leg-', 'harbor-gantry-runway-', 'harbor-shed-roof', 'harbor-rack-', 'harbor-reefer-', 'harbor-truck-', 'harbor-gatehouse-',
            'harbor-office-', 'harbor-gangway-', 'harbor-stack-q-', 'harbor-stack-n-c-2', 'harbor-stack-s-w-2', 'harbor-stack-s-e-2'))

# ---------------------------------------------------------------------------------------------------------------------------------------------
def hazard_rects():
    """2D footprints of every hazard volume (x0, x1, z0, z1, name): the painted crane slots and the water's kill volume."""
    out = []
    for j, zn in enumerate(crane['zones']):
        c = zn['center']; out.append((c['x'] - PAINT_ZONE[0] / 2, c['x'] + PAINT_ZONE[0] / 2, c['z'] - PAINT_ZONE[1] / 2, c['z'] + PAINT_ZONE[1] / 2, 'crane-zone-%d' % j))
    for k in KILL:
        c, s = k['center'], k['size']; out.append((c['x'] - s['x'] / 2, c['x'] + s['x'] / 2, c['z'] - s['z'] / 2, c['z'] + s['z'] / 2, k['id']))
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
    for e in els:                                                        # elevated pieces first (deck, gangways, open flights), the ground over them
        b = e['box']
        if b[2] >= 2.0 and e['ch'] in 'D=/r': rect(b[0], b[1], b[4], b[5], e['ch'])
    for x0, x1, z0, z1, name in hazard_rects():
        if name.startswith('crane'): rect(x0, x1, z0, z1, 'Z')
    for e in sorted(els, key=lambda e: e['box'][3]):
        b = e['box']
        if b[2] >= 2.0 or e['ch'] in '#Ru': continue
        rect(b[0], b[1], b[4], b[5], e['ch'])
    for e in els:
        if e['ch'] == '#': b = e['box']; rect(b[0], b[1], b[4], b[5], e['ch'])
    for i, (x, z) in enumerate(teamA): put(x, z, str(i + 1)); put(-x, z, str(i + 5))
    for nm, (x, z) in zip(ffa_names[2:], ffa[2:]): put(x, z, nm)
    out = ['         x=-42' + ' ' * 37 + 'x=0' + ' ' * 37 + 'x=+41   (water beyond the top row)']
    for r in range(R): z = Z1 - 2 * r; out.append(f'{z-2:+4d}..{z:+3d} ' + ''.join(g[r]))
    return '\n'.join(out)

# ---- line of sight: 3D segment against boxes; a box only blocks if it tops out at least 2.0 m above the lower eye's floor
def seg_hits(a, b, e, floor):
    x0, x1, y0, y1, z0, z1 = e['box']
    if y1 < floor + 2.0: return False
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
def visible(a, b, floor): return not any(seg_hits(a, b, e, floor) for e in els)
def spawn_floor(p): return 0.0                                           # every spawn of this draft stands on the quay / yard slab
def check(pairs, label):
    bad = []
    for (na, a), (nb, b) in pairs:
        fa, fb = spawn_floor(a), spawn_floor(b); lo = min(fa, fb)
        n = sum(1 for dx, dz, ex, ez in itertools.product((-1, 0, 1), repeat=4) for eye in (1.2, 1.6, 1.9)
                if visible((a[0] + dx, fa + eye, a[1] + dz), (b[0] + ex, fb + eye, b[1] + ez), lo))
        if n: bad.append(f'{na}-{nb} ({n}/243 variants open, {math.dist(a, b):.1f} m)')
    print(f'{label}: {len(pairs)} pairs, {len(pairs) - len(bad)} fully blocked under +-1 m jitter x 3 eye heights')
    for s in bad: print('   OPEN', s)
    return len(bad)
def dist_rect(p, r): dx = max(r[0] - p[0], 0, p[0] - r[1]); dz = max(r[2] - p[1], 0, p[1] - r[3]); return math.hypot(dx, dz)

# ---- reachability: a 0.25 m layered raster. A cell is standable at a block top (or the slab) if nothing occupies the 1.8 m above its centre and
#      no block intrudes the 0.42 m capsule ring between top+0.45 and top+1.8; neighbours connect when |dy| <= 0.25. Flood fill from spawn A1.
CELL = .25
def raster():
    NX, NZ = int((X1 - X0) / CELL), int((Z1 - Z0) / CELL)
    exact, ring = {}, {}
    for e in els:
        x0, x1, y0, y1, z0, z1 = e['box']
        for i in range(max(0, int((x0 - CAPSULE - X0) / CELL) - 1), min(NX, int((x1 + CAPSULE - X0) / CELL) + 2)):
            cx = X0 + (i + .5) * CELL; dx = max(x0 - cx, 0, cx - x1)
            if dx >= CAPSULE: continue
            for j in range(max(0, int((z0 - CAPSULE - Z0) / CELL) - 1), min(NZ, int((z1 + CAPSULE - Z0) / CELL) + 2)):
                cz = Z0 + (j + .5) * CELL; dz = max(z0 - cz, 0, cz - z1)
                if dx * dx + dz * dz >= CAPSULE * CAPSULE: continue
                ring.setdefault((i, j), []).append((y0, y1))
                if dx == 0 and dz == 0: exact.setdefault((i, j), []).append((y0, y1, e['id']))
    lim = (BOUNDS['minX'] + CAPSULE, BOUNDS['maxX'] - CAPSULE, BOUNDS['minZ'] + CAPSULE, BOUNDS['maxZ'] - CAPSULE)
    floors = {}                                                          # (i, j) -> {height: block id}
    for i in range(NX):
        cx = X0 + (i + .5) * CELL
        if not lim[0] <= cx <= lim[1]: continue
        for j in range(NZ):
            cz = Z0 + (j + .5) * CELL
            if not lim[2] <= cz <= lim[3]: continue
            cand = {0.0: 'harbor-floor'}
            for y0, y1, bid in exact.get((i, j), ()): cand[round(y1, 3)] = bid
            ok = {}
            for h, bid in cand.items():
                if any(y0 < h + 1.8 and y1 > h + 1e-6 for y0, y1, _ in exact.get((i, j), ())): continue
                if any(y0 < h + 1.8 and y1 > h + .45 for y0, y1 in ring.get((i, j), ())): continue
                ok[h] = bid
            if ok: floors[(i, j)] = ok
    def cell(p): return (int((p[0] - X0) / CELL), int((p[1] - Z0) / CELL))
    start = (cell(teamA[0]), 0.0); seen = {start}; todo = [start]
    while todo:
        (i, j), h = todo.pop()
        for di, dj in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            n = (i + di, j + dj)
            for h2 in floors.get(n, ()):
                if abs(h2 - h) <= .25 + 1e-6 and (n, h2) not in seen: seen.add((n, h2)); todo.append((n, h2))
    return floors, seen, cell

def unity_json():
    def block(e):
        x0, x1, y0, y1, z0, z1 = e['box']
        return {'id': e['id'], 'material': e['mat'], 'pos': v(round((x0 + x1) / 2, 3), round((y0 + y1) / 2, 3), round((z0 + z1) / 2, 3)),
                'size': v(round(x1 - x0, 3), round(y1 - y0, 3), round(z1 - z0, 3)), 'rotation': {'x': 0, 'y': 0, 'z': 0, 'w': 1}, 'role': e['role'], 'mass': 26, 'hp': e['hp']}
    sp = lambda p: {'pos': v(p[0], round(spawn_floor(p) + 1.1, 3), p[1]), 'yaw': round(yaw(p), 3)}
    return {'schema': 1, 'materials': [],
            'maps': [{'id': 'harbor', 'name': 'Pier 9', 'tag': 'Pier 9, night shift. Watch the hook, mind the edge.', 'theme': 'harbor',
                      'music': 'harbor-night-shift', 'ambience': 'harbor', 'look': 'harbor-night', 'sky': 0x0E1726, 'floor': 0x3B3F45, 'accent': 0xF2A23A,
                      'rotation': False, 'bounds': BOUNDS, 'spawns': {'ffa': [sp(p) for p in ffa], 'teams': [{'side': 0, 'points': [sp(p) for p in teamA]}, {'side': 1, 'points': [sp(p) for p in teamB]}]},
                      'blocks': [block(FLOOR)] + [block(e) for e in els], 'segments': [], 'pilotBlocks': PILOT, 'hazards': HAZ, 'killVolumes': KILL,
                      'nav': {'nodes': [], 'links': []}}]}

def png(path, scale=8):
    W, H = (X1 - X0) * scale, (Z1 - Z0 + 6) * scale; ZT = Z1 + 6          # 6 m of the basin drawn above the quay edge
    img = bytearray(b'\x18\x1a\x1e' * W * H)
    def fill(x0, x1, z0, z1, c, alpha=1.0):
        i0, i1 = int((x0 - X0) * scale), int((x1 - X0) * scale); j0, j1 = int((ZT - z1) * scale), int((ZT - z0) * scale)
        for j in range(max(0, j0), min(H, j1)):
            for i in range(max(0, i0), min(W, i1)):
                o = (j * W + i) * 3
                for k in range(3): img[o + k] = int(img[o + k] * (1 - alpha) + c[k] * alpha)
    fill(X0, X1, Z1, ZT, (16, 34, 52))
    fill(X0, X1, Z0, Z1, (58, 60, 64))
    fill(-40, 40, -30, -10, (72, 70, 66))                                # under the shed roof
    col = {'#': (110, 112, 116), 'k': (220, 190, 40), 'o': (40, 40, 44), 'L': (200, 120, 40), 's': (170, 100, 40), 'D': (190, 120, 50), 'r': (240, 200, 60),
           '/': (170, 170, 175), 'n': (70, 120, 160), 'N': (150, 70, 55), 'M': (60, 120, 80), '=': (200, 200, 205), 'T': (120, 125, 140), 'G': (150, 140, 120),
           'O': (150, 140, 120), 'X': (110, 92, 70), 'x': (190, 140, 80), 'c': (90, 110, 150), 'f': (230, 170, 30), 'C': (60, 60, 70), 'h': (100, 105, 110), 'H': (80, 85, 95), 'g': (200, 200, 120)}
    for e in sorted(els, key=lambda e: e['box'][3]):
        b = e['box']
        if e['ch'] in 'Ru': continue
        fill(b[0], b[1], b[4], b[5], col.get(e['ch'], (128, 128, 128)), .7 if b[2] >= 2.0 else 1.0)
    for x0, x1, z0, z1, name in hazard_rects():
        if name.startswith('crane'): fill(x0, x1, z0, z1, (240, 190, 30), .55)
    for i, p in enumerate(teamA + teamB):
        fill(p[0] - .6, p[0] + .6, p[1] - .6, p[1] + .6, (60, 200, 255) if i < 4 else (255, 90, 120))
    for p in ffa[2:]: fill(p[0] - .5, p[0] + .5, p[1] - .5, p[1] + .5, (240, 240, 240))
    raw = b''.join(b'\x00' + bytes(img[j * W * 3:(j + 1) * W * 3]) for j in range(H))
    ch = lambda t, d: struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    with open(path, 'wb') as f: f.write(b'\x89PNG\r\n\x1a\n' + ch(b'IHDR', struct.pack('>IIBBBBB', W, H, 8, 2, 0, 0, 0)) + ch(b'IDAT', zlib.compress(raw, 9)) + ch(b'IEND', b''))

if __name__ == '__main__':
    print(paint())
    fails = 0
    named = list(zip(ffa_names, ffa)); fails += check(list(itertools.combinations(named, 2)), 'FFA')
    A = [(f'A{i+1}', p) for i, p in enumerate(teamA)]; B = [(f'B{i+1}', p) for i, p in enumerate(teamB)]; fails += check([(p, q) for p in A for q in B], 'Teams A-B')
    sp = ffa + teamA[1:] + teamB[1:]
    print('min FFA spacing %.1f m; min A-B spacing %.1f m' % (min(math.dist(p, q) for p, q in itertools.combinations(ffa, 2)), min(math.dist(p, q) for p in teamA for q in teamB)))
    worst = min((dist_rect(p, r[:4]), r[4], p) for p in sp for r in hazard_rects())
    print('closest spawn to any hazard volume: %.1f m (%s, spawn %s)' % worst); fails += worst[0] < 3.0
    clr = min((min(dist_rect(p, (e['box'][0], e['box'][1], e['box'][4], e['box'][5])) for e in els if e['box'][2] < 1.9), p) for p in sp)
    print('closest spawn to any block: %.1f m at %s' % clr); fails += clr[0] < CAPSULE
    inb = all(BOUNDS['minX'] + CAPSULE <= x <= BOUNDS['maxX'] - CAPSULE and BOUNDS['minZ'] + CAPSULE <= z <= BOUNDS['maxZ'] - CAPSULE for x, z in sp)
    print('all spawns inside the bounds minus the capsule:', inb); fails += not inb
    print('mirror check (team B = team A mirrored, FFA set mirror-closed):', teamB == [(-x, z) for x, z in teamA] and all((-x, z) in ffa for x, z in ffa))
    # the painted slots keep 0.5 m from any block; the cross aisle (between the truck caps) holds no prop
    zc = []
    for x in ZONES:
        r = (x - PAINT_ZONE[0] / 2, x + PAINT_ZONE[0] / 2, ZONE_Z - PAINT_ZONE[1] / 2, ZONE_Z + PAINT_ZONE[1] / 2)
        for e in els:
            b = e['box']
            if b[2] >= 2.4: continue
            dx = max(r[0] - b[1], 0, b[0] - r[1]); dz = max(r[2] - b[5], 0, b[4] - r[3]); zc.append((math.hypot(dx, dz), e['id'], x))
    zmin = min(zc); print('closest block to a painted drop slot: %.2f m (%s, slot x=%+d)' % zmin); fails += zmin[0] < .5
    lane = [e['id'] for e in els if e['box'][2] < 2.4 and e['box'][4] < AISLE[1] and e['box'][5] > AISLE[0] and e['box'][1] > -28 and e['box'][0] < 28]
    print('props in the cross aisle (z %.1f..%.1f, x -28..28):' % AISLE, lane or 'none'); fails += bool(lane)
    # nothing standable overhangs the quay edge
    over = [e['id'] for e in els if e['box'][5] > 30.0 + 1e-6]; print('blocks past the quay edge (z > 30):', over or 'none'); fails += bool(over)
    # reachability on foot (layered raster) and the pilot blocks over every standable top nobody can walk to
    floors, seen, cell = raster()
    lv = {}
    for (c, h) in seen: lv[h] = lv.get(h, 0) + 1
    tot = {}
    for c, hs in floors.items():
        for h in hs: tot[h] = tot.get(h, 0) + 1
    named_lv = [(0.0, 'quay/yard/shed slab'), (2.6, 'container terraces'), (5.2, 'container tops'), (9.0, 'STS portal deck')]
    print('reach raster (%.2f m cells): reachable / standable cells per level:' % CELL, ', '.join('%s %.1f m %d/%d' % (n, h, lv.get(h, 0), tot.get(h, 0)) for h, n in named_lv),
          '; stairs and kerb %d' % sum(n for h, n in lv.items() if h not in dict(named_lv)))
    for h, n in named_lv:
        if not lv.get(h): print('   UNREACHABLE level', n, h); fails += 1
    unreach = [n for n, p in list(zip(ffa_names, ffa)) + [(f'A{i+1}', p) for i, p in enumerate(teamA)] + [(f'B{i+1}', p) for i, p in enumerate(teamB)] if (cell(p), 0.0) not in seen]
    print('every spawn reachable on foot from A1:', not unreach, unreach or ''); fails += bool(unreach)
    reach_by_block = {}
    for (c, h) in seen:
        bid = floors[c][h]; reach_by_block[bid] = reach_by_block.get(bid, 0) + 1
    stand_by_block = {}
    for c, hs in floors.items():
        for h, bid in hs.items(): stand_by_block[bid] = stand_by_block.get(bid, 0) + 1
    def covered(e):
        x0, x1, y0, y1, z0, z1 = e['box']
        for p in PILOT:
            c, s = p['center'], p['size']
            if c['x'] - s['x'] / 2 <= x0 + .01 and c['x'] + s['x'] / 2 >= x1 - .01 and c['z'] - s['z'] / 2 <= z0 + .01 and c['z'] + s['z'] / 2 >= z1 - .01 and y1 - .5 <= p['bottom'] <= y1 + .05: return True
        return False
    loose = [e['id'] for e in els if e['box'][3] >= 2.5 and stand_by_block.get(e['id']) and not reach_by_block.get(e['id']) and not covered(e)]
    rails = [e['id'] for e in els if '-rail-' in e['id'] and not covered(e)]
    walk_tops = sorted({e['id'].rsplit('-', 1)[0] for e in els if e['box'][3] >= 2.5 and reach_by_block.get(e['id'])})
    steps = {}
    for e in els:
        if e['id'].startswith('harbor-stair-'): g = e['id'].rsplit('-', 1)[0]; st = steps.setdefault(g, [0, 0]); st[0] += 1; st[1] += bool(reach_by_block.get(e['id']))
    short = [g for g, (n, r) in steps.items() if r < n]
    print('stair flights: %d, every step reached on foot: %s' % (len(steps), not short), short or ''); fails += bool(short)
    mid = [e['id'] for e in els if 1.3 <= e['box'][3] - e['box'][2] <= 2.2 and e['box'][2] < .01 and not e['id'].startswith('harbor-stair-')]
    print('tops between 1.3 and 2.2 m (stairs excepted):', mid or 'none'); fails += bool(mid)
    print('walkable tops reached on foot:', len(walk_tops), 'groups;', 'standable tops nobody can walk to, without a pilot block:', loose or 'none',
          '; rails without a pilot column:', rails or 'none'); fails += bool(loose) + bool(rails)
    # spawn exposure from the power positions: every reachable standing spot 2.5 m or more up (deck, container tops, terraces, flights; 2 m sampling)
    # plus the whole portal deck (5 x 5 eye points). Team spawns: none on the enemy half may see them (spawn jitter +-1 m, eye 1.6); FFA: reported
    vps = {}
    for (c, fl) in seen:
        if fl >= 2.5: x = X0 + (c[0] + .5) * CELL; z = Z0 + (c[1] + .5) * CELL; vps.setdefault((round(x / 2), round(z / 2), fl), (x, fl + 1.6, z))
    vps = list(vps.values()) + [(ex, 10.6, 24 + ez) for ex, ez in itertools.product((-4, -2, 0, 2, 4), (-2.5, -1, 0, 1, 2.5))]
    tall = [e for e in els if e['box'][3] >= 2.0]
    def seen_from(p, src): return sum(1 for s in src for dx, dz in ((0, 0), (1, 0), (-1, 0), (0, 1), (0, -1)) if not any(seg_hits(s, (p[0] + dx, 1.6, p[1] + dz), e, 0.0) for e in tall))
    tex = [(n, seen_from(p, [s for s in vps if s[0] * p[0] < 0 or abs(s[1] - 10.6) < 1e-6])) for n, p in A + B]
    fex = [(n, seen_from(p, vps)) for n, p in named[2:]]
    print('spawn exposure (open variants) from %d elevated spots: team (enemy half + deck) %s; FFA (all) %s' % (len(vps), ', '.join('%s %d' % t for t in tex), ', '.join('%s %d' % t for t in fex)))
    fails += any(n for _, n in tex)
    # long sightlines: the longest open run along x at eye 1.6 per lane, and full-map diagonals (x -39 -> +39, |dz| >= 20) at eye 1.6
    def run_x(z):
        best, cur, x = 0, 0, -39.5
        while x < 39.5:
            if visible((x, 1.6, z), (x + .5, 1.6, z), 0.0): cur += .5; best = max(best, cur)
            else: cur = 0
            x += .5
        return best
    for nm, zs in (('quay', (14.5, 17, 19, 20.4, 22, 25.5, 28, 29.3)), ('gantry aisle', (2.0, 4.0, 6.0)), ('road', (-6, -8, -9.8)), ('shed', (-12, -15, -18.6, -22.6, -25, -28.5))):
        print('longest open E-W line at eye 1.6, %s: %s' % (nm, ', '.join('z%+.1f %.0f m' % (z, run_x(z)) for z in zs)))
    def run_z(x):
        best, cur, z = 0, 0, -29.5
        while z < 29.5:
            if visible((x, 1.6, z), (x, 1.6, z + .5), 0.0): cur += .5; best = max(best, cur)
            else: cur = 0
            z += .5
        return best
    print('longest open N-S line at eye 1.6: %s' % ', '.join('x%+.1f %.0f m' % (x, run_z(x)) for x in (-34, -26, -23.1, -16, -7.7, -2, 0)))
    diag = [((-39, za), (39, zb)) for za in range(-29, 30, 2) for zb in range(-29, 30, 2) if abs(za - zb) >= 20 and visible((-39, 1.6, za), (39, 1.6, zb), 0.0)]
    print('full-map diagonal sightlines (x -39 -> +39, |dz| >= 20, eye 1.6):', diag or 'none'); fails += bool(diag)
    # hazard fairness numbers
    c = crane; legs = [abs(ZONES[k] - ZONES[k - 1]) for k in range(len(ZONES))]; first = abs(ZONES[0] - (c['a']['x'] + c['b']['x']) / 2)
    busy = c['travel'] + c['warn'] + c['rise'] + c['dwell'] + REGRIP + c['hoist']
    q = math.sqrt(1 - c['zones'][0]['size']['y'] / c['carry']); lethal = c['rise'] * (1 - q) + .06
    print('gantry drop: %d slots at x %s (visit order), every %.0f s, first impact at fight + %.1f s; travel %.1f s per leg (%d-%d m: %.1f-%.1f m/s, peak %.1f; first leg %d m from park)'
          % (len(ZONES), ZONES, c['period'], 4 + c['start'] + c['travel'] + c['warn'] + c['rise'], c['travel'], min(legs), max(legs), min(legs) / c['travel'], max(legs) / c['travel'], 1.5 * max(legs) / c['travel'], first))
    print('   telegraph %.1f s strobe over the slot + fall %.2f s from %.1f m = %.2f s from red to impact; lethal about %.2f s per cycle; landed cover %.1f s (dwell) + %.1f s regrip = %.1f s;'
          ' hoist %.1f s; motion %.1f of %.0f s' % (c['warn'], c['rise'], c['carry'], c['warn'] + c['rise'], lethal, c['dwell'], REGRIP, c['dwell'] + REGRIP, c['hoist'], busy, c['period']))
    fails += busy > c['period'] + 1e-6
    print('   load %.1f x %.1f x %.1f m (lying along x); kill box %.1f x %.1f x %.1f m; painted slot %.1f x %.1f m; slot gaps %.1f m; landed gaps %.1f m'
          % (LOAD['x'], LOAD['y'], LOAD['z'], c['zones'][0]['size']['x'], c['zones'][0]['size']['y'], c['zones'][0]['size']['z'], PAINT_ZONE[0], PAINT_ZONE[1], 8 - PAINT_ZONE[0], 8 - LOAD['x']))
    print('quay edge: kill volume %s (x -42..42, y -8..-1, z 30.2..60), proposed in killVolumes; kerb 0.2 m, no parapet' % KILL[0]['id'])
    print('blocks: %d (+ floor); hazards: %d; kill volumes: %d; pilot blocks: %d; spawns: %d FFA + %d + %d' % (len(els), len(HAZ), len(KILL), len(PILOT), len(ffa), len(teamA), len(teamB)))
    fails += len(els) > 420
    ov = [(a['id'], b['id']) for a, b in itertools.combinations(els, 2) if all(min(a['box'][k + 1], b['box'][k + 1]) - max(a['box'][k], b['box'][k]) > 1e-3 for k in (0, 2, 4))]
    print('overlapping blocks:', ov or 'none'); fails += bool(ov)
    ids = [e['id'] for e in els]; dup = {i for i in ids if ids.count(i) > 1}
    if dup: print('DUPLICATE ids:', sorted(dup)); fails += 1
    if '--json' in sys.argv:
        with open(sys.argv[sys.argv.index('--json') + 1], 'w') as f: json.dump(unity_json(), f, separators=(',', ':'))
    if '--out' in sys.argv:
        out = sys.argv[sys.argv.index('--out') + 1]; os.makedirs(out, exist_ok=True)
        with open(os.path.join(out, 'harbor_m1.json'), 'w') as f: json.dump(unity_json(), f, separators=(',', ':'))
        png(os.path.join(out, 'harbor_m1.png')); print('wrote', os.path.join(out, 'harbor_m1.json'), 'and harbor_m1.png')
    print('checks:', 'all pass' if not fails else '%d FAILED' % fails)
    sys.exit(1 if fails else 0)
