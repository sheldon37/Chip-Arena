# The Hollow Sanctum (sanctum: a castle's great chapel at night, 60x40, a hall church with a crossing, three levels) draft
# painter, 3D spawn LOS, level raster, hazard clearance and sightline checks.
# Coordinates: x east, z north, y up, metres; Unity frame. Point symmetric: (x,z)->(-x,-z) in collision (team A holds the west
# end under the organ loft, team B the east end under the reliquary loft: same boxes, different dressing). Levels: the crypt
# floor -3.6, the church floor 0, the galleries 4.0. The vault (21) and the walls close the room: the stained-glass windows are
# openings at 8 m and up (out of reach: 4.0 + 3.4), so the moon key falls through them onto the floors.
# usage: python3 docs/arenas/scripts/sanctum_m1.py [--out DIR] [--json PATH]   (stdout: plans + checks; --json writes the draft only;
#        --out writes sanctum_m1.json, draft map data in the UnityMaps.json shape, and sanctum_m1.png, the three level plans)
# Ids carry the prefixes the presentation keys its looks and kit on (sanctum-pier-*, sanctum-wall-*, sanctum-gallery-*,
# sanctum-pew-*, sanctum-stair-*, sanctum-crypt-*, ...); geometry, spawns and hazard timing become sim-owned once integrated.
import math, json, itertools, sys, os, zlib, struct
X0, X1, Z0, Z1 = -30, 30, -20, 20          # the bounds are the interior: the nave's end walls and the transept's end walls
BOUNDS = {'minX': -30, 'maxX': 30, 'minZ': -20, 'maxZ': 20, 'ceiling': 22, 'killY': -10}
GF, GL, CR = 0.0, 4.0, -3.6                # floor tops: the church, the galleries, the crypt (slabs 0.4 m)
AW = 11.5                                  # the aisle walls' inner face |z|; the nave's piers stand on |z| 6 (5.3..6.7)
AX = 6.0                                   # the transept's side walls' inner face |x| (the arms run to |z| 20)
VAULT = 21.0                               # the vault's springing: the roof slab 21..21.6 closes the hall
CAP = .42
els = []; PILOT = []   # els: id, mat, role, box (x0,x1,y0,y1,z0,z1), ch, hp; ch 'r' = see-through balustrade (never a sight blocker)
SWAP = {'n': 's', 's': 'n', 'w': 'e', 'e': 'w', 'a': 'b', 'b': 'a'}
def mirror_id(i): return '-'.join(SWAP.get(t, t) for t in i.split('-'))
def v(x, y, z): return {'x': round(x, 3), 'y': round(y, 3), 'z': round(z, 3)}
def mbox(b): x0, x1, y0, y1, z0, z1 = b; return (-x1, -x0, y0, y1, -z1, -z0)
def pilot(i, b):   # a pilot-only column from just under the top (bots and chips cannot stand there)
    PILOT.append(dict(id=i.replace('sanctum-', 'sanctum-top-', 1), center=v((b[0] + b[1]) / 2, 0, (b[4] + b[5]) / 2), size=v(b[1] - b[0], 0, b[5] - b[4]), bottom=round(b[3] - .1, 3)))
def add(i, x0, x1, y0, y1, z0, z1, ch, mat='stone', role='static', hp=0, mirror=True, top=False):
    b = (x0, x1, y0, y1, z0, z1); els.append(dict(id=i, mat=mat, role=role, box=b, ch=ch, hp=hp))
    if top: pilot(i, b)
    if mirror:
        m = mirror_id(i); assert m != i, 'mirror of %s keeps its id' % i
        els.append(dict(id=m, mat=mat, role=role, box=mbox(b), ch=ch, hp=hp))
        if top: pilot(m, mbox(b))
def rail(i, x0, x1, z0, z1, y, mirror=True): add(i, x0, x1, y, y + 1.0, z0, z1, 'r', mirror=mirror)   # a 1.0 m stone balustrade
def stair(i, a, b, w0, w1, ya, yb, steps, base, axis='x', mirror=True):
    # a closed flight (each step solid from `base`, no pocket under it) from (a, ya) to (b, yb); along x (w0..w1 is z) or along z
    run = (b - a) / steps; rise = (yb - ya) / steps
    for k in range(steps):
        s0, s1 = sorted((a + run * k, a + run * (k + 1))); y1 = ya + rise * (k + 1)
        if axis == 'x': add('%s-%02d' % (i, k), s0, s1, base, y1, w0, w1, '/', mirror=mirror)
        else: add('%s-%02d' % (i, k), w0, w1, base, y1, s0, s1, '/', mirror=mirror)
def rect_minus(r, holes):
    """(x0,x1,z0,z1) minus hole rects, as x strips merged where their z pieces match (deterministic, so symmetric input stays symmetric)."""
    xs = sorted(set([r[0], r[1]] + [h[k] for h in holes for k in (0, 1) if r[0] < h[k] < r[1]])); strips = []
    for xa, xb in zip(xs, xs[1:]):
        zs = [(r[2], r[3])]
        for h in holes:
            if h[0] < xb - 1e-9 and h[1] > xa + 1e-9:
                zs = [p for za, zb in zs for p in ([(za, zb)] if h[3] <= za or h[2] >= zb else [q for q in ((za, h[2]), (h[3], zb)) if q[1] - q[0] > 1e-9])]
        if strips and strips[-1][2] == zs: strips[-1][1] = xb
        else: strips.append([xa, xb, zs])
    return [(xa, xb, za, zb) for xa, xb, zs in strips for za, zb in zs]
def wall_run(i, x0, x1, y0, y1, z0, z1, sill, head, wins, along='x'):
    """A wall from y0 to y1 with window openings (sill..head) centred at `wins` (centre, half width) along its length."""
    if along == 'x':
        add(i + '-lo', x0, x1, y0, sill, z0, z1, '#'); add(i + '-hi', x0, x1, head, y1, z0, z1, '#')
        edges = [x0] + [e for c, h in wins for e in (c - h, c + h)] + [x1]
        for k in range(0, len(edges), 2):
            if edges[k + 1] - edges[k] > 1e-6: add('%s-m%d' % (i, k // 2), edges[k], edges[k + 1], sill, head, z0, z1, '#')
    else:
        add(i + '-lo', x0, x1, y0, sill, z0, z1, '#'); add(i + '-hi', x0, x1, head, y1, z0, z1, '#')
        edges = [z0] + [e for c, h in wins for e in (c - h, c + h)] + [z1]
        for k in range(0, len(edges), 2):
            if edges[k + 1] - edges[k] > 1e-6: add('%s-m%d' % (i, k // 2), x0, x1, sill, head, edges[k], edges[k + 1], '#')

# ---------------------------------------------------------------------------------------------------------------------------
# ---- the church floor (top 0): the cross (nave and choir |z| <= 11.5 over the whole length, the transept |x| <= 6 to |z| 20)
#      minus the two crypt stairwells (x -14..-8, z 2.2..4.2 and its partner), which drop to the crypt under the crossing
WELL_W = (-14.0, -8.0, 2.2, 4.2)
WELLS = [WELL_W, (-WELL_W[1], -WELL_W[0], -WELL_W[3], -WELL_W[2])]
for k, (x0, x1, z0, z1) in enumerate(rect_minus((X0, X1, -AW, AW), WELLS)): add('sanctum-floor-%d' % k, x0, x1, -.4, GF, z0, z1, '.', mat='foundation', mirror=False)
add('sanctum-floor-arm-n', -AX, AX, -.4, GF, AW, Z1, '.', mat='foundation')

# ---- the crypt (top -3.6) under the crossing: 16 x 9.2 m, 3.2 m under the crossing's slab; walls to the slab's underside
CX, CZ = 8.0, 4.6
add('sanctum-crypt-floor', -CX, CX, -4.0, CR, -CZ, CZ, '.', mat='foundation', mirror=False)
add('sanctum-crypt-wall-n', -CX - .4, CX + .4, -4.0, -.4, CZ, CZ + .4, 'W')
add('sanctum-crypt-wall-w-0', -CX - .4, -CX, -4.0, -.4, -CZ, WELL_W[2], 'W'); add('sanctum-crypt-wall-w-1', -CX - .4, -CX, -4.0, -.4, WELL_W[3], CZ, 'W')
# the crypt stairs: 18 risers of 0.2 m x 0.333 m from the nave floor down into the crypt's west door (B: the east door); walled
stair('sanctum-stair-crypt-w', WELL_W[0], WELL_W[1], WELL_W[2], WELL_W[3], GF, CR, 18, -4.0)
add('sanctum-crypt-stairwall-n-w', WELL_W[0], -CX - .4, -4.0, -.4, WELL_W[3], WELL_W[3] + .2, 'W'); add('sanctum-crypt-stairwall-s-w', WELL_W[0], -CX - .4, -4.0, -.4, WELL_W[2] - .2, WELL_W[2], 'W')
rail('sanctum-balustrade-well-n-w', WELL_W[0], WELL_W[1], WELL_W[3], WELL_W[3] + .25, GF); rail('sanctum-balustrade-well-s-w', WELL_W[0], WELL_W[1], WELL_W[2] - .25, WELL_W[2], GF)
rail('sanctum-balustrade-well-e-w', WELL_W[1], WELL_W[1] + .25, WELL_W[2] - .25, WELL_W[3] + .25, GF)   # the crossing's edge over the door
# the crypt: four short pillars under the crossing, two tomb chests a side (half cover), the reliquary shrine at the centre
add('sanctum-crypt-pillar-n-w', -4.4, -3.6, CR, -.4, 2.0, 2.8, 'P'); add('sanctum-crypt-pillar-s-w', -4.4, -3.6, CR, -.4, -2.8, -2.0, 'P')
add('sanctum-crypt-tomb-s-w', -7.0, -5.0, CR, CR + 1.05, -3.9, -3.0, 'x'); add('sanctum-crypt-tomb-n-w', -2.6, -1.6, CR, CR + 1.05, 2.4, 4.2, 'x')
add('sanctum-crypt-shrine', -1.6, 1.6, CR, CR + 2.6, -1.2, 1.2, 'S', mirror=False)

# ---- the shell: aisle walls (|z| 11.5..12.3) to the vault with tall lancets in every bay (sills at 8 m: out of reach), the end
#      walls (|x| 30) with the great rose windows over the lofts, the transept's side and end walls (the south rose faces the moon)
BAYS = (-27.0, -21.0, -15.0, -9.0)
wall_run('sanctum-wall-aisle-n-w', X0 - .8, -AX - .8, -.4, VAULT, AW, AW + .8, 8.0, 18.0, [(c, 1.2) for c in BAYS])
wall_run('sanctum-wall-aisle-s-w', X0 - .8, -AX - .8, -.4, VAULT, -AW - .8, -AW, 8.0, 18.0, [(c, 1.2) for c in BAYS])
wall_run('sanctum-wall-end-w', X0 - .8, X0, -.4, VAULT, -AW, AW, 10.0, 18.0, [(0.0, 4.0)], along='z')
wall_run('sanctum-wall-arm-n-w', -AX - .8, -AX, -.4, VAULT, AW, Z1, 8.0, 18.0, [(16.0, 1.2)], along='z')
wall_run('sanctum-wall-arm-s-w', -AX - .8, -AX, -.4, VAULT, -Z1, -AW, 8.0, 18.0, [(-16.0, 1.2)], along='z')
wall_run('sanctum-wall-armend-n', -AX - .8, AX + .8, -.4, VAULT, Z1, Z1 + .8, 9.0, 17.0, [(0.0, 3.8)])
# the vault: one slab over the hall with the oculus over the crossing (the bell and the censer's rope hang through it)
OCULUS = (-2.0, 2.0, -2.0, 2.0)
for k, (x0, x1, z0, z1) in enumerate(rect_minus((X0 - .8, X1 + .8, -AW - .8, AW + .8), [OCULUS])): add('sanctum-vault-%d' % k, x0, x1, VAULT, VAULT + .6, z0, z1, 'V', mirror=False)
add('sanctum-vault-arm-n', -AX - .8, AX + .8, VAULT, VAULT + .6, AW + .8, Z1 + .8, 'V')
PILOT.append(dict(id='sanctum-top-vault', center=v(0, 0, 0), size=v(X1 - X0 + 1.6, 0, Z1 - Z0 + 1.6), bottom=19.0))   # nothing stands up there

# ---- the piers: the arcade (|z| 6, every 6 m) and the four crossing piers (1.8 m), free-standing to the vault (full cover)
for k, x in enumerate((-24.0, -18.0, -12.0)):
    add('sanctum-pier-n-w-%d' % k, x - .7, x + .7, GF, VAULT, 5.3, 6.7, 'P'); add('sanctum-pier-s-w-%d' % k, x - .7, x + .7, GF, VAULT, -6.7, -5.3, 'P')
add('sanctum-pier-cross-n-w', -6.9, -5.1, GF, VAULT, 5.1, 6.9, 'P'); add('sanctum-pier-cross-s-w', -6.9, -5.1, GF, VAULT, -6.9, -5.1, 'P')

# ---- the galleries (top 4.0): the organ loft over the narthex (A; the reliquary loft over the sanctuary is B), a gallery over
#      each aisle behind the arcade, the rood loft over the nave at the crossing's west arch, the bridges over the transept's
#      aisle passages: one loop round the crossing at 4 m. Stone balustrades (1.0 m) on every edge over a drop.
STAIR_N = (X0, X0 + 2.0, 1.5, 7.5)                  # the narthex stair's well through the organ loft (x, z)
for k, (x0, x1, z0, z1) in enumerate(rect_minus((X0, -24.7, -AW, AW), [STAIR_N])): add('sanctum-gallery-loft-w-%d' % k, x0, x1, 3.6, GL, z0, z1, '=')
add('sanctum-gallery-n-w', -24.7, -6.9, 3.6, GL, 6.7, AW, '='); add('sanctum-gallery-s-w', -24.7, -6.9, 3.6, GL, -AW, -6.7, '=')
add('sanctum-gallery-rood-w', -9.0, -6.9, 3.6, GL, -6.7, 6.7, '=')
add('sanctum-gallery-bridge-n', -6.9, 6.9, 3.6, GL, 6.9, AW, '=')
rail('sanctum-balustrade-n-w', -24.7, -9.0, 6.7, 6.95, GL); rail('sanctum-balustrade-s-w', -24.7, -9.0, -6.95, -6.7, GL)
rail('sanctum-balustrade-loft-w', -24.95, -24.7, -6.7, 6.7, GL)
rail('sanctum-balustrade-rood-w-0', -9.0, -8.75, -6.7, 6.7, GL); rail('sanctum-balustrade-rood-w-1', -7.15, -6.9, -5.1, 5.1, GL)
rail('sanctum-balustrade-bridge-s-n', -5.1, 5.1, 6.9, 7.15, GL)                       # over the crossing (its partner: the south bridge)
ARM_STAIR = (-5.8, -3.8)                                                              # the north arm's stair lands on the bridge here
rail('sanctum-balustrade-bridge-n-0', -AX, ARM_STAIR[0], AW - .25, AW, GL); rail('sanctum-balustrade-bridge-n-1', ARM_STAIR[1], AX, AW - .25, AW, GL)
# stairs to the galleries: 20 risers of 0.2 m x 0.3 m, 2.0 m wide; the narthex stair (A's own, up the west wall to the organ
# loft) and the north arm's stair up to the north bridge (its partner, the south arm's, belongs to B's half)
stair('sanctum-stair-loft-w', STAIR_N[2], STAIR_N[3], STAIR_N[0], STAIR_N[1], GF, GL, 20, GF, axis='z')
stair('sanctum-stair-arm-n', 17.5, AW, ARM_STAIR[0], ARM_STAIR[1], GF, GL, 20, GF, axis='z')

# ---- the narthex (A) under the organ loft: the west screen (blind arcading; the reredos at B) closes it from the nave, two
#      short columns carry the loft; it opens to both aisles. The font before the screen (B: the high altar before the reredos).
add('sanctum-screen-w', -24.7, -24.1, GF, 3.6, -5.3, 5.3, 'W')
add('sanctum-column-loft-n-w', -27.8, -27.0, GF, 3.6, 5.6, 6.4, 'P'); add('sanctum-column-loft-s-w', -27.8, -27.0, GF, 3.6, -6.4, -5.6, 'P')
add('sanctum-altar-w', -22.6, -21.6, GF, 1.1, -1.6, 1.6, 'A')
# ---- the nave's pews (A; the choir stalls at B): two banks of five either side of the central aisle (half cover, breakable)
for k, x in enumerate((-22.2, -19.6, -17.0)):
    add('sanctum-pew-n-w-%d' % k, x - .55, x + .55, GF, 1.0, 1.9, 5.0, 'p', mat='wood', role='cover', hp=60)
for k, x in enumerate((-19.6, -17.0, -14.4, -11.8)):
    add('sanctum-pew-s-w-%d' % k, x - .55, x + .55, GF, 1.0, -5.0, -1.9, 'p', mat='wood', role='cover', hp=60)
# ---- the aisles: a confessional and a saint's plinth a side break the 4.8 m aisles into a slalom (full cover, unstandable)
add('sanctum-confessional-n-w', -22.2, -19.8, GF, 2.6, 9.1, AW, 'C'); add('sanctum-plinth-n-w', -13.0, -11.0, GF, 2.6, 6.7, 9.1, 'C')
add('sanctum-confessional-s-w', -16.2, -13.8, GF, 2.6, -AW, -9.1, 'C'); add('sanctum-plinth-s-w', -22.0, -20.0, GF, 2.6, -9.1, -6.7, 'C')
# ---- the transept's north arm (A's flank; the south arm is B's): the founder's tomb (a canopied chest, 3.0 m) and a low tomb
add('sanctum-tomb-n', -2.0, 2.0, GF, 3.0, 14.4, 16.2, 'T'); add('sanctum-tomb-low-n', 1.9, 4.1, GF, 1.05, 18.3, 19.2, 'x')

# ---------------------------------------------------------------------------------------------------------------------------
# ---- hazards (UnityMaps.json `hazards[]`); every kind exists (NativeFactoryHazards: crane, saw-rail; NativeWardZeroHazards:
#      burner, shutter). Factory clocks: `start` s after the arm time (FightAt + 4 s). Ward clocks: `first` s after FightAt.
def hz(**k):
    d = dict(id='', kind='', a=v(0, 0, 0), b=v(0, 0, 0), axis=v(0, 0, 0), size=v(0, 0, 0), center=v(0, 0, 0), radius=0, start=0, period=0, offset=0,
             warn=0, rise=0, travel=0, dwell=0, hoist=0, carry=0, damage=0, tick=0, push=0, first=0, danger=0, speed=0, length=0, charge=0, live=0,
             lethal=False, alternate=False, zones=[])
    d.update(k); return d
def mv(p): return v(-p['x'], p['y'], -p['z'])
# The Fall of the Lights: four iron chandeliers over the central aisle (the crane kind, one served a cycle, alternating halves).
# The served chandelier creaks and sways for 3.2 s, falls 9 m in 1.1 s (lethal under it), lies 4.5 s as half cover, the winch
# takes it up again. Zones in visiting order (odd rounds start one later).
CHAND = [(-12.6, 0.0), (12.6, 0.0), (-19.2, 0.0), (19.2, 0.0)]
CHANDELIERS = hz(id='sanctum-chandeliers', kind='crane', a=v(-19.2, 14.0, 0), b=v(19.2, 14.0, 0), size=v(2.8, 1.0, 2.8), start=4, period=15, warn=3.2, rise=1.1,
                 travel=1.5, dwell=4.5, hoist=2.8, carry=9.0, lethal=True, zones=[{'center': v(x, GF, z), 'size': v(3.2, 2.2, 3.2)} for x, z in CHAND])
# The Thurible: the great censer swings north-south through the crossing on its 20 m rope (the saw-rail kind: parked drawn back
# over the south arch, released, a swing to the north arch, a beat, the swing back). Lethal on its path while it swings.
CENSER = hz(id='sanctum-censer', kind='saw-rail', a=v(0, 1.3, -5.0), b=v(0, 1.3, 5.0), axis=v(1, 0, 0), size=v(1.4, 2.2, 1.4), radius=.7, start=2, period=12,
            warn=2.2, travel=1.7, dwell=.3, lethal=True)
# Holy fire: the fire baskets under each transept bridge pour two curtains across the aisle passage (the burner kind; the pocket
# between them is safe). The north and south passages fire in turns, one every 5 s.
BY = 1.7
FIRE_N = hz(id='sanctum-fire-n', kind='burner', center=v(0, BY, 9.2), size=v(7.0, 3.4, 4.6), axis=v(1, 0, 0), first=6, period=10, offset=5, warn=1.6, live=2.5,
            dwell=1.0, tick=.25, damage=8, zones=[{'center': v(x, BY, 9.2), 'size': v(1.8, 3.4, 4.6)} for x in (-2.6, 2.6)])
FIRE_S = dict(FIRE_N, id='sanctum-fire-s', center=mv(FIRE_N['center']), first=11, offset=-5, zones=[{'center': mv(z['center']), 'size': z['size']} for z in FIRE_N['zones']])
# The Toll: the great bell rings three times and the crypt's iron gates fall (the shutter kind: a collider door with push and
# the interlock). Both gates on the bell's one clock: the crypt seals for 8 s every 32 s.
GATE_W = hz(id='sanctum-gate-w', kind='shutter', center=v(-8.2, -2.0, 3.2), size=v(.3, 3.2, 2.0), axis=v(0, 0, 1), first=14, period=32, offset=0, warn=3.0,
            travel=1.0, dwell=8.0, rise=1.5, push=6)
GATE_E = dict(GATE_W, id='sanctum-gate-e', center=mv(GATE_W['center']))
HAZ = [CHANDELIERS, CENSER, FIRE_N, FIRE_S, GATE_W, GATE_E]

# ---- spawns (x, z, floor top); yaw from +z toward +x. Team A in the narthex under the organ loft, B the mirror under the
#      reliquary loft. FFA: the two end rooms, the transept arms' bays, the aisles by the crossing, the crypt's corners.
teamA = [(-26.0, 3.0, GF), (-26.0, -3.0, GF), (-28.8, -2.0, GF), (-28.6, 0.4, GF)]
teamB = [(-x, -z, f) for x, z, f in teamA]
ffa_names = ['A1', 'B1', 'f', 'F', 'g', 'G', 'h', 'H']
ffa = [teamA[0], teamB[0], (0.0, 18.6, GF), (0.0, -18.6, GF), (-10.0, 9.5, GL), (10.0, -9.5, GL), (-6.2, -1.2, CR), (6.2, 1.2, CR)]
def yaw_of(p, name):
    if name.startswith('A') or p in teamA: return 90.0
    if name.startswith('B') or p in teamB: return 270.0
    return (math.degrees(math.atan2(-p[0], -p[1])) + 360) % 360

def hazard_rects():
    """2D footprints of every hazard volume (x0, x1, z0, z1, name, y0, y1): what spawns keep 3 m from."""
    out = []
    for h in HAZ:
        if h['kind'] in ('burner', 'shutter'):
            c, s = h['center'], h['size']; out.append((c['x'] - s['x'] / 2, c['x'] + s['x'] / 2, c['z'] - s['z'] / 2, c['z'] + s['z'] / 2, h['id'], c['y'] - s['y'] / 2, c['y'] + s['y'] / 2))
        elif h['kind'] == 'crane':
            for z in h['zones']:
                c, s = z['center'], z['size']; out.append((c['x'] - s['x'] / 2, c['x'] + s['x'] / 2, c['z'] - s['z'] / 2, c['z'] + s['z'] / 2, h['id'], c['y'], c['y'] + s['y']))
        else:
            a, b, s = h['a'], h['b'], h['size']
            out.append((min(a['x'], b['x']) - s['x'] / 2, max(a['x'], b['x']) + s['x'] / 2, min(a['z'], b['z']) - s['z'] / 2, max(a['z'], b['z']) + s['z'] / 2, h['id'], a['y'] - s['y'] / 2, a['y'] + s['y'] / 2))
    return out

# ---- 3D sight lines: eyes 1.2/1.6/1.9 m over each spawn's own floor; a box blocks where the segment passes through it in x, y and z.
#      Boxes topping out under 2.0 m above the lower floor do not count (half cover does not hide a standing pilot); balustrades never do.
def seg_hits(a, b, e, low):
    x0, x1, y0, y1, z0, z1 = e['box']
    if e['ch'] == 'r' or y1 < low + 2.0: return False
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
SOLID = [e for e in els if e['ch'] != 'r']
def visible(a, b, low, blocks=None):
    bx0, bx1, bz0, bz1 = min(a[0], b[0]), max(a[0], b[0]), min(a[2], b[2]), max(a[2], b[2])
    for e in blocks or SOLID:
        x0, x1, y0, y1, z0, z1 = e['box']
        if x1 < bx0 or x0 > bx1 or z1 < bz0 or z0 > bz1: continue
        if seg_hits(a, b, e, low): return False
    return True
def check(pairs, label):
    bad = []
    for (na, a), (nb, b) in pairs:
        low = min(a[2], b[2])
        n = sum(1 for dx, dz, ex, ez in itertools.product((-1, 0, 1), repeat=4) for eye in (1.2, 1.6, 1.9)
                if visible((a[0] + dx, a[2] + eye, a[1] + dz), (b[0] + ex, b[2] + eye, b[1] + ez), low))
        if n: bad.append(f'{na}-{nb} ({n}/243 variants open, {math.dist(a[:2], b[:2]):.1f} m)')
    print(f'{label}: {len(pairs)} pairs, {len(pairs) - len(bad)} fully blocked under +-1 m jitter x 3 eye heights (3D, per-floor eyes)')
    for s in bad: print('   OPEN', s)
    return len(bad)
def dist_rect(p, r): dx = max(r[0] - p[0], 0, p[0] - r[1]); dz = max(r[2] - p[1], 0, p[1] - r[3]); return math.hypot(dx, dz)

# ---- 3D level raster: 0.25 m cells. A cell is standable on a block top if nothing sits on that top at the cell centre, there is
#      1.8 m headroom and no block intrudes the 0.42 m capsule ring between top+0.45 and top+1.8; neighbours connect when |dy| <= 0.25
#      (the 0.2 m risers) or equal. Flood fill from spawn A1. Pilot columns stop it (nothing stands under one's bottom + 1.7).
CELL = .25; NX, NZ = int((X1 - X0) / CELL), int((Z1 - Z0) / CELL)
def raster():
    cols = {}
    for e in els:
        x0, x1, y0, y1, z0, z1 = e['box']
        for i in range(max(0, int((x0 - CAP - X0) / CELL) - 1), min(NX, int((x1 + CAP - X0) / CELL) + 2)):
            cx = X0 + (i + .5) * CELL; dx = max(x0 - cx, 0, cx - x1)
            if dx >= CAP: continue
            for j in range(max(0, int((z0 - CAP - Z0) / CELL) - 1), min(NZ, int((z1 + CAP - Z0) / CELL) + 2)):
                cz = Z0 + (j + .5) * CELL; dz = max(z0 - cz, 0, cz - z1)
                if dx * dx + dz * dz < CAP * CAP: cols.setdefault((i, j), []).append((y0, y1, x0 < cx < x1 and z0 < cz < z1))
    surf = {}
    for c, lst in cols.items():
        cx, cz = X0 + (c[0] + .5) * CELL, Z0 + (c[1] + .5) * CELL
        for y0, y1, inside in lst:
            if not inside: continue
            t = y1
            if any(inn and b0 < t + .45 and b1 > t + .01 for b0, b1, inn in lst): continue           # something sits on this top
            if any(b0 < t + 1.8 and b1 > t + .45 for b0, b1, inn in lst): continue                  # headroom / capsule ring
            if any(t + 1.7 >= p['bottom'] and abs(cx - p['center']['x']) < p['size']['x'] / 2 + CAP and abs(cz - p['center']['z']) < p['size']['z'] / 2 + CAP for p in PILOT): continue
            surf.setdefault(c, set()).add(round(t, 3))
    start = None; a = ffa[0]
    c = (int((a[0] - X0) / CELL), int((a[1] - Z0) / CELL))
    for t in surf.get(c, ()):
        if abs(t - a[2]) < .05: start = (c, t)
    seen = {start} if start else set(); todo = list(seen)
    while todo:
        (i, j), t = todo.pop()
        for di, dj in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            n = (i + di, j + dj)
            for u in surf.get(n, ()):
                if abs(u - t) <= .25 + 1e-6 and (n, u) not in seen: seen.add((n, u)); todo.append((n, u))
    return surf, seen
def level(t): return 'crypt' if abs(t - CR) < .05 else 'floor' if abs(t - GF) < .05 else 'gallery' if abs(t - GL) < .05 else 'other'
def at(surf, seen, p):
    c = (int((p[0] - X0) / CELL), int((p[1] - Z0) / CELL)); return any((c, t) in seen for t in surf.get(c, ()) if abs(t - p[2]) < .05)

def sightlines(surf, seen):
    out = {}
    for lv, y in (('floor', GF), ('gallery', GL)):
        pts = sorted({(round(X0 + (i + .5) * CELL), round(Z0 + (j + .5) * CELL)) for (i, j), t in seen if abs(t - y) < .05 and i % 8 == 4 and j % 8 == 4})
        blocks = [e for e in SOLID if e['box'][3] >= y + 2.0 and e['box'][2] < y + 1.9]
        best, diag = (0, None, None), (0, None, None)
        for p, q in itertools.combinations(pts, 2):
            d = math.dist(p, q)
            if d < 30 or (d <= best[0] and (abs(p[1] - q[1]) < 14 or d <= diag[0])): continue
            if visible((p[0], y + 1.6, p[1]), (q[0], y + 1.6, q[1]), y, blocks):
                if d > best[0]: best = (d, p, q)
                if abs(p[1] - q[1]) >= 14 and abs(p[0] - q[0]) >= 14 and d > diag[0]: diag = (d, p, q)
        out[lv] = (best, diag)
    return out

def flights(surf, seen):
    groups, out = {}, []
    for e in els:
        if e['ch'] == '/' and e['id'][-3] == '-' and e['id'][-2:].isdigit(): groups.setdefault(e['id'][:-3], []).append(e)
    for name, steps in sorted(groups.items()):
        own = {}
        for k, e in enumerate(steps):
            x0, x1, y0, y1, z0, z1 = e['box']
            for i in range(int((x0 - X0) / CELL), int((x1 - X0) / CELL) + 1):
                for j in range(int((z0 - Z0) / CELL), int((z1 - Z0) / CELL) + 1):
                    if x0 < X0 + (i + .5) * CELL < x1 and z0 < Z0 + (j + .5) * CELL < z1: own[(i, j)] = (k, round(y1, 3))
        nodes = {(c, t) for c, (k, t) in own.items() if t in surf.get(c, ())}
        bottom = {n for n in nodes if own[n[0]][0] == 0}; top = {n for n in nodes if own[n[0]][0] == len(steps) - 1}
        got = set(bottom); todo = list(bottom)
        while todo:
            (i, j), t = todo.pop()
            for di, dj in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                c = (i + di, j + dj)
                if c in own and (c, own[c][1]) in nodes and abs(own[c][1] - t) <= .25 + 1e-6 and (c, own[c][1]) not in got: got.add((c, own[c][1])); todo.append((c, own[c][1]))
        out.append((name, len(steps), bool(top & got) and bool(bottom & seen) and bool(top & seen)))
    return out

def clear_volume(c, s, ignore=()):
    """Blocks intruding a hazard's live volume (centre, size): the crane zones, the censer's path and the fire curtains stand clear."""
    x0, x1, y0, y1, z0, z1 = c['x'] - s['x'] / 2, c['x'] + s['x'] / 2, c['y'] - s['y'] / 2, c['y'] + s['y'] / 2, c['z'] - s['z'] / 2, c['z'] + s['z'] / 2
    return [e['id'] for e in els if e['id'] not in ignore and e['box'][0] < x1 - 1e-3 and e['box'][1] > x0 + 1e-3 and e['box'][2] < y1 - 1e-3 and e['box'][3] > y0 + 1e-3 and e['box'][4] < z1 - 1e-3 and e['box'][5] > z0 + 1e-3]

def unity_json():
    def block(e):
        x0, x1, y0, y1, z0, z1 = e['box']
        return {'id': e['id'], 'material': e['mat'], 'pos': v((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), 'size': v(x1 - x0, y1 - y0, z1 - z0),
                'rotation': {'x': 0, 'y': 0, 'z': 0, 'w': 1}, 'role': e['role'], 'mass': 26, 'hp': e['hp']}
    names = dict((id(p), n) for n, p in zip(ffa_names, ffa))
    sp = lambda p, n='': {'pos': v(p[0], p[2] + 1.1, p[1]), 'yaw': round(yaw_of(p, n), 3)}
    return {'schema': 1, 'materials': [],
            'maps': [{'id': 'sanctum', 'name': 'The Hollow Sanctum', 'tag': 'The castle chapel at midnight. Mind the lights.', 'theme': 'sanctum',
                      'music': 'sanctum-theme', 'ambience': 'sanctum-night', 'look': 'sanctum-midnight', 'sky': 0x0A0C18, 'floor': 0x4A4440, 'accent': 0xE8B24A,
                      'rotation': True, 'bounds': BOUNDS,
                      'spawns': {'ffa': [sp(p, n) for n, p in zip(ffa_names, ffa)], 'teams': [{'side': 0, 'points': [sp(p, 'A') for p in teamA]}, {'side': 1, 'points': [sp(p, 'B') for p in teamB]}]},
                      'blocks': [block(e) for e in els], 'segments': [], 'pilotBlocks': PILOT, 'hazards': HAZ,
                      'nav': {'nodes': [], 'links': []}}]}

def png(path, surf, seen, scale=8):
    PW, GAP = (X1 - X0) * scale, 16; PH = (Z1 - Z0) * scale; W, H = 3 * PW + 2 * GAP, PH; img = bytearray(b'\x0c\x10\x20' * W * H)
    def fill(k, x0, x1, z0, z1, c, alpha=1.0):
        ox = k * (PW + GAP); i0, i1 = int((x0 - X0) * scale), int(math.ceil((x1 - X0) * scale)); j0, j1 = int((Z1 - z1) * scale), int(math.ceil((Z1 - z0) * scale))
        for j in range(max(0, j0), min(H, j1)):
            for i in range(max(0, i0), min(PW, i1)):
                o = (j * W + ox + i) * 3
                for q in range(3): img[o + q] = int(img[o + q] * (1 - alpha) + c[q] * alpha)
    for k in range(3): fill(k, X0, X1, Z0, Z1, (14, 18, 34))
    floor = {CR: (0, (70, 66, 78)), GF: (1, (96, 92, 88)), GL: (2, (120, 108, 92))}
    for (i, j), t in surf.items():
        for u in t:
            for y, (k, c) in floor.items():
                if abs(u - y) < .05: fill(k, X0 + i * CELL, X0 + (i + 1) * CELL, Z0 + j * CELL, Z0 + (j + 1) * CELL, c if ((i, j), u) in seen else (120, 50, 50))
    col = {'#': (150, 140, 130), 'P': (190, 180, 160), 'W': (130, 125, 120), 'r': (220, 200, 150), '/': (185, 185, 190), 'p': (150, 100, 60), 'C': (110, 80, 60),
           'T': (200, 190, 170), 'x': (170, 160, 140), 'A': (230, 200, 120), 'S': (220, 190, 110), '=': (120, 108, 92), 'V': (40, 40, 60)}
    for e in sorted(els, key=lambda e: (e['ch'] == 'r', e['box'][3])):
        b = e['box']
        if e['ch'] in '.=V': continue
        k = 0 if b[2] < -.5 else 1 if b[2] < 3.5 else 2
        if k == 1 and b[3] > 8: fill(2, b[0], b[1], b[4], b[5], col.get(e['ch'], (128, 128, 128)))
        fill(k, b[0], b[1], b[4], b[5], col.get(e['ch'], (128, 128, 128)))
    for x0, x1, z0, z1, name, h0, h1 in hazard_rects():
        k = 0 if h0 < -.5 else 1
        fill(k, x0, x1, z0, z1, (255, 120, 20) if 'fire' in name else (255, 60, 60) if 'chand' in name else (200, 120, 255) if 'censer' in name else (120, 200, 255), .45)
    for idx, p in enumerate(teamA + teamB):
        k = 0 if p[2] < -.5 else 1 if p[2] < 3.5 else 2; fill(k, p[0] - .6, p[0] + .6, p[1] - .6, p[1] + .6, (60, 200, 255) if idx < 4 else (255, 90, 120))
    for p in ffa[2:]:
        k = 0 if p[2] < -.5 else 1 if p[2] < 3.5 else 2; fill(k, p[0] - .5, p[0] + .5, p[1] - .5, p[1] + .5, (245, 245, 245))
    raw = b''.join(b'\x00' + bytes(img[j * W * 3:(j + 1) * W * 3]) for j in range(H))
    ch = lambda t, d: struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    with open(path, 'wb') as f: f.write(b'\x89PNG\r\n\x1a\n' + ch(b'IHDR', struct.pack('>IIBBBBB', W, H, 8, 2, 0, 0, 0)) + ch(b'IDAT', zlib.compress(raw, 9)) + ch(b'IEND', b''))

if __name__ == '__main__':
    surf, seen = raster()
    named = list(zip(ffa_names, ffa)); bad = check(list(itertools.combinations(named, 2)), 'FFA')
    A = [(f'A{i+1}', p) for i, p in enumerate(teamA)]; B = [(f'B{i+1}', p) for i, p in enumerate(teamB)]; bad += check([(p, q) for p in A for q in B], 'Teams A-B')
    d3 = lambda p, q: math.dist((p[0], p[2], p[1]), (q[0], q[2], q[1]))
    print('min FFA spacing %.1f m (plan; %.1f m 3D); min A-B spacing %.1f m' % (min(math.dist(p[:2], q[:2]) for p, q in itertools.combinations(ffa, 2)),
          min(d3(p, q) for p, q in itertools.combinations(ffa, 2)), min(math.dist(p[:2], q[:2]) for p in teamA for q in teamB)))
    sp = ffa + teamA[1:] + teamB[1:]
    worst = min((dist_rect(p, r[:4]) if r[5] - 2 < p[2] < r[6] + 1 else 99, r[4], p[:2]) for p in sp for r in hazard_rects())
    print('closest spawn to any hazard volume on its level (plan footprint): %.1f m (%s, spawn %s)' % worst); bad += worst[0] < 3
    clr = min((min(dist_rect(p[:2], (e['box'][0], e['box'][1], e['box'][4], e['box'][5])) for e in els if e['box'][2] < p[2] + 1.9 and e['box'][3] > p[2] + .3), p[:2]) for p in sp)
    print('closest spawn to any block at its floor: %.2f m at %s' % clr); bad += clr[0] < CAP
    inb = all(X0 + CAP <= p[0] <= X1 - CAP and Z0 + CAP <= p[1] <= Z1 - CAP for p in sp); print('all spawns inside the bounds minus the capsule:', inb); bad += not inb
    lv = {}
    for c, ts in surf.items():
        for t in ts: k = level(t); lv.setdefault(k, [0, 0]); lv[k][1] += 1; lv[k][0] += (c, t) in seen
    print('level raster (0.25 m cells) reachable on foot from A1: ' + '; '.join('%s %d of %d' % (k, lv[k][0], lv[k][1]) for k in ('crypt', 'floor', 'gallery', 'other') if k in lv))
    reach = {n: at(surf, seen, p) for n, p in list(zip(ffa_names, ffa)) + A + B}
    print('every spawn reachable:', all(reach.values()), '' if all(reach.values()) else [n for n, r in reach.items() if not r]); bad += not all(reach.values())
    levels = all(lv.get(k, [0])[0] > 0 for k in ('crypt', 'floor', 'gallery')); print('every level reachable:', levels); bad += not levels
    lost = {k: lv[k][1] - lv[k][0] for k in ('crypt', 'floor', 'gallery') if k in lv}; print('walkable cells not reached on the three levels:', lost)
    for k, ((d, p, q), (dd, pp, qq)) in sightlines(surf, seen).items():
        print('longest clear sightline, %s (eyes 1.6 m, 2 m sample grid): %.1f m %s-%s; longest diagonal (|dx|,|dz| >= 14): %s' % (k, d, p, q, '%.1f m %s-%s' % (dd, pp, qq) if pp else 'none of 30 m or more'))
    fl = flights(surf, seen); okf = all(r[2] for r in fl)
    print('stairs: %d flights, each walkable bottom to top on its own and joined to both levels: %s (%s)' % (len(fl), okf, ', '.join('%s %s' % (n.replace('sanctum-stair-', ''), 'ok' if r else 'BROKEN') for n, k, r in fl)))
    bad += not okf or len(fl) != 6
    key = lambda b: tuple(round(c, 3) for c in b)
    sym = sorted(key(e['box']) for e in els) == sorted(key(mbox(e['box'])) for e in els)
    psym = sorted((round(p['center']['x'], 3), round(p['center']['z'], 3), round(p['size']['x'], 3), round(p['size']['z'], 3), p['bottom']) for p in PILOT) == \
           sorted((round(-p['center']['x'], 3), round(-p['center']['z'], 3), round(p['size']['x'], 3), round(p['size']['z'], 3), p['bottom']) for p in PILOT)
    ssym = sorted(teamB) == sorted((-x, -z, f) for x, z, f in teamA) and sorted(ffa) == sorted((-x, -z, f) for x, z, f in ffa)
    print('point symmetric (x,z)->(-x,-z): blocks %s, pilot blocks %s, spawns %s' % (sym, psym, ssym)); bad += not (sym and psym and ssym)
    band = [e['id'] for e in els if e['ch'] not in '/r' and any(1.3 < e['box'][3] - f < 2.2 for f in (CR, GF, GL) if e['box'][2] <= f + .01 <= e['box'][3])]
    print('tops in the 1.3-2.2 m band over their floor:', band or 'none'); bad += len(band) > 0
    # hazard fairness
    for h in HAZ:
        if h['kind'] == 'crane':
            for z in h['zones']:
                c = dict(z['center']); c['y'] = z['center']['y'] + z['size']['y'] / 2; hits = clear_volume(c, z['size'])
                if hits: print('   crane zone at', z['center'], 'intruded by', hits); bad += 1
        if h['kind'] == 'saw-rail':
            c = v((h['a']['x'] + h['b']['x']) / 2, h['a']['y'], (h['a']['z'] + h['b']['z']) / 2); s = v(abs(h['a']['x'] - h['b']['x']) + h['size']['x'], h['size']['y'], abs(h['a']['z'] - h['b']['z']) + h['size']['z'])
            hits = clear_volume(c, s)
            if hits: print('   censer path intruded by', hits); bad += 1
        if h['kind'] == 'burner':
            for z in h['zones']:
                hits = clear_volume(z['center'], z['size'])
                if hits: print('   fire curtain intruded by', hits); bad += 1
    ch = CHANDELIERS; cyc = ch['travel'] + ch['warn'] + ch['rise'] + ch['dwell'] + 1.5 + ch['hoist']
    print('chandeliers: %d zones, one falls every %.0f s (each every %.0f s), first warning at FightAt + %.1f s; creak %.1f s, fall %.1f s from %.1f m, %.1f s as cover; motion %.1f of %.0f s'
          % (len(ch['zones']), ch['period'], ch['period'] * len(ch['zones']), 4 + ch['start'] + ch['travel'], ch['warn'], ch['rise'], ch['carry'], ch['dwell'], cyc, ch['period'])); bad += cyc > ch['period'] or ch['warn'] < 3
    c = CENSER; busy = c['warn'] + 2 * c['travel'] + c['dwell']
    print('censer: every %.0f s from FightAt + %.1f s; drawn back %.1f s, swing %.1f s each way through a %.1f s beat, lethal %.1f s of %.0f s; path |z| <= %.1f at x 0'
          % (c['period'], 4 + c['start'], c['warn'], c['travel'], c['dwell'], 2 * c['travel'] + c['dwell'], c['period'], abs(c['b']['z']) + c['size']['z'] / 2)); bad += busy > c['period'] or c['warn'] < 1.6
    f = FIRE_N; cross = (f['zones'][0]['size']['x'] + 2 * CAP) / 6.8
    print('holy fire: N first %.0f s (even rounds +%.0f), S first %.0f s (even rounds %.0f): one passage every %.0f s; warn %.1f s, live %.1f s, smoke %.1f s; crossing a curtain %.2f s = %d ticks x %d; a whole burst %d'
          % (f['first'], f['offset'], FIRE_S['first'], FIRE_S['offset'], f['period'] / 2, f['warn'], f['live'], f['dwell'], cross, math.ceil(cross / f['tick']), f['damage'], int(f['live'] / f['tick']) * f['damage']))
    okz = all(abs(z['center'][k] - h['center'][k]) + z['size'][k] / 2 <= h['size'][k] / 2 + 1e-3 for h in HAZ if h['kind'] == 'burner' for z in h['zones'] for k in 'xyz')
    okt = all(h['first'] >= 4 and h['first'] + h['offset'] >= 4 and h['warn'] >= 1.5 and h['warn'] + h['live'] + h['dwell'] <= h['period'] and not h['lethal'] for h in HAZ if h['kind'] == 'burner'); bad += not (okz and okt)
    g = GATE_W; print('crypt gates: the toll from FightAt + %.0f s every %.0f s: %.1f s of bells, %.1f s falling, %.1f s sealed, %.1f s rising; push %.0f m/s' % (g['first'], g['period'], g['warn'], g['travel'], g['dwell'], g['rise'], g['push']))
    oks = all(h['warn'] + h['travel'] + h['dwell'] + h['rise'] <= h['period'] and min(h['size']['x'], h['size']['z']) < 1 < max(h['size']['x'], h['size']['z']) and h['first'] >= 4 for h in HAZ if h['kind'] == 'shutter')
    print('burner zones inside their volumes: %s; burner clocks: %s; shutters: %s' % (okz, okt, oks)); bad += not oks
    ids = [e['id'] for e in els]; dup = len(ids) - len(set(ids)); print('duplicate block ids: %d' % dup); bad += dup > 0
    print('blocks: %d; breakable: %d; hazards: %d; pilot blocks: %d; spawns: %d FFA, %d + %d team' % (len(els), sum(e['role'] == 'cover' for e in els), len(HAZ), len(PILOT), len(ffa), len(teamA), len(teamB)))
    bad += len(els) > 350
    if '--json' in sys.argv:
        with open(sys.argv[sys.argv.index('--json') + 1], 'w') as f: json.dump(unity_json(), f, separators=(',', ':'))
    if '--out' in sys.argv:
        out = sys.argv[sys.argv.index('--out') + 1]; os.makedirs(out, exist_ok=True)
        with open(os.path.join(out, 'sanctum_m1.json'), 'w') as f: json.dump(unity_json(), f, separators=(',', ':'))
        png(os.path.join(out, 'sanctum_m1.png'), surf, seen); print('wrote', os.path.join(out, 'sanctum_m1.json'), 'and sanctum_m1.png')
    print('checks:', 'PASS' if not bad else 'FAIL (%d)' % bad)
    sys.exit(1 if bad else 0)
