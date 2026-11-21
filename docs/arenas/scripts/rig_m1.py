# Deepwell M1 (rig: an offshore production platform in a force-nine storm, 56x44, three decks) draft painter, 3D spawn LOS,
# deck raster, hazard clearance and sightline checks.
# Coordinates: x east, z north, y up, metres; Unity frame. Point symmetric: (x,z)->(-x,-z) unless noted. Deck tops: cellar 0
# (grating over the sea), main 4.2, drill floor 8.4. The sea is presentation only and is drawn at y -14; killY -6 covers it,
# so 'killVolumes' stays []. The bounds clamp pilots at +-(28|22 - 0.42), so the sea drops are notches cut into the deck edge.
# usage: python3 docs/arenas/scripts/rig_m1.py [--out DIR] [--json PATH]   (stdout: plans + checks; --json writes the draft only;
#        --out writes rig_m1.json, draft map data in the UnityMaps.json shape, and rig_m1.png, the three deck plans, 8 px per metre)
# A draft for Astra (docs/arenas/NEW-STAGES.md), NOT wired into the game. Ids carry the prefixes the presentation keys its looks
# and kit dressing on (rig-deck-*, rig-rail-*, rig-stair-*, rig-accom-*, rig-module-*, rig-kodrum-*, rig-derrick-*, ...);
# geometry, spawns and hazard timing become sim-owned (Astra) once integrated.
import math, json, itertools, sys, os, zlib, struct
X0, X1, Z0, Z1 = -28, 28, -22, 22           # the cellar deck is the whole footprint; the bounds are the footprint
BOUNDS = {'minX': -28, 'maxX': 28, 'minZ': -22, 'maxZ': 22, 'ceiling': 22, 'killY': -6}
CD, MD, DF = 0.0, 4.2, 8.4                   # deck tops: cellar, main, drill floor (slabs 0.3 m)
MX, MZ = 23.5, 17.5                         # main deck half extents: the cellar walkway ring (4.5 m) is open to the sky outside it
RAIL = 1.1; CAP = .42
els = []; PILOT = []   # els dict: id, mat, role, box (x0,x1,y0,y1,z0,z1), ch, hp; ch 'r' = see-through handrail (never a sight blocker)
SWAP = {'n': 's', 's': 'n', 'w': 'e', 'e': 'w', 'a': 'b', 'b': 'a'}
def mirror_id(i): return '-'.join(SWAP.get(t, t) for t in i.split('-'))
def v(x, y, z): return {'x': round(x, 3), 'y': round(y, 3), 'z': round(z, 3)}
def mbox(b): x0, x1, y0, y1, z0, z1 = b; return (-x1, -x0, y0, y1, -z1, -z0)
def pilot(i, b):   # an infinite pilot-only column from just under the top (bots and chips cannot stand or vault there)
    PILOT.append(dict(id=i.replace('rig-', 'rig-top-', 1), center=v((b[0] + b[1]) / 2, 0, (b[4] + b[5]) / 2), size=v(b[1] - b[0], 0, b[5] - b[4]), bottom=round(b[3] - .1, 3)))
def add(i, x0, x1, y0, y1, z0, z1, ch, mat='metal', role='static', hp=0, mirror=True, top=False):
    b = (x0, x1, y0, y1, z0, z1); els.append(dict(id=i, mat=mat, role=role, box=b, ch=ch, hp=hp))
    if top: pilot(i, b)
    if mirror:
        m = mirror_id(i); assert m != i, 'mirror of %s keeps its id' % i
        els.append(dict(id=m, mat=mat, role=role, box=mbox(b), ch=ch, hp=hp))
        if top: pilot(m, mbox(b))
def rail(i, x0, x1, z0, z1, y, mirror=True): add(i, x0, x1, y, y + RAIL, z0, z1, 'r', top=True, mirror=mirror)   # 1.1 m, 0.1 m thick
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

# ---- cellar deck (top 0): 0.3 m grating over the whole footprint, minus the four sea notches (3 m wide, 2 m deep) at the outer edge.
#      The bounds clamp keeps pilots off the true edge, so each boat landing / lifeboat station is a notch: the rail gap opens
#      onto a 3 x 2 m drop to the sea (a knockback kill), with a beacon post (telltale) at its inboard corner.
NOTCH_N = {'lifeboat': (-20.0, -17.0), 'landing': (11.0, 14.0)}       # north edge (x ranges); point-symmetric partners on the south edge
ND = 20.0                                                             # notch inner edge |z| (3 x 2 m drops; 2.5 m of ring passes inboard)
FLOOR = dict(id='rig-deck-cellar', mat='foundation', role='static', box=(X0, X1, -.3, 0, -ND, ND), ch='.', hp=0)
for k, (x0, x1, z0, z1) in enumerate(rect_minus((X0, X1, ND, Z1), list((a, b, ND, Z1) for a, b in NOTCH_N.values()))):
    add('rig-deck-cellar-n-%d' % k, x0, x1, -.3, 0, z0, z1, '.', mat='foundation')
NOTCHES = [(nm + '-n', a, b, ND, Z1) for nm, (a, b) in NOTCH_N.items()] + [(nm + '-s', -b, -a, -Z1, -ND) for nm, (a, b) in NOTCH_N.items()]
# outer handrails (the bounds clamp already holds pilots, the rails and their pilot columns keep bots from vaulting at it)
for k, (x0, x1, z0, z1) in enumerate(rect_minus((X0, X1, Z1 - .1, Z1), list((a, b, Z1 - .1, Z1) for a, b in NOTCH_N.values()))):
    rail('rig-rail-cellar-n-%d' % k, x0, x1, z0, z1, CD)
rail('rig-rail-cellar-w', X0, X0 + .1, -Z1 + .1, Z1 - .1, CD)
for nm, (a, b) in NOTCH_N.items():   # the notch's two side edges are railed; its inner 3 m edge is the open drop (a knockback kill)
    rail('rig-rail-%s-n-w' % nm, a - .1, a, ND, Z1 - .1, CD); rail('rig-rail-%s-n-e' % nm, b, b + .1, ND, Z1 - .1, CD)
    add('rig-telltale-%s-n' % nm, a - .45, a - .1, 0, 2.6, ND - .35, ND, '!', top=True)   # beacon post at the open edge (strobe + stripes)

# ---- main deck (top 4.2) over x +-23.5, z +-17.5 with two wells (railed openings onto the cellar) so the levels read
WELL_NW = (-14.5, -9.5, 8.0, 12.0)
HOLES = [WELL_NW, (-WELL_NW[1], -WELL_NW[0], -WELL_NW[3], -WELL_NW[2])]
for k, (x0, x1, z0, z1) in enumerate(rect_minus((-MX, MX, -MZ, MZ), HOLES)): add('rig-deck-main-%d' % k, x0, x1, 3.9, MD, z0, z1, '.', mirror=False)
rail('rig-rail-well-n-w-0', -14.6, -9.4, 12.0, 12.1, MD); rail('rig-rail-well-n-w-1', -14.6, -9.4, 7.9, 8.0, MD)
rail('rig-rail-well-n-w-2', -14.6, -14.5, 8.0, 12.0, MD); rail('rig-rail-well-n-w-3', -9.5, -9.4, 8.0, 10.0, MD)   # the drum closes z 10..12
# main deck perimeter rails (gaps where the ring stairs land; the accommodation's wall is the edge where it stands)
rail('rig-rail-main-n-0', -MX, -12.6, MZ - .1, MZ, MD); rail('rig-rail-main-n-1', -10.8, MX, MZ - .1, MZ, MD)
rail('rig-rail-main-w-0', -MX, -MX + .1, 8.8, MZ - .1, MD); rail('rig-rail-main-w-1', -MX, -MX + .1, -MZ + .1, -7.0, MD)

# ---- ring stairs, cellar to main deck, at both ends (west A, east B) and both sides (north, south): 21 risers of 0.2 m x 0.3 m,
#      1.8 m wide against the main deck edge; a landing at the top, a stair-tower wall on the sea side, a skirt under the deck edge
stair('rig-stair-ring-w', 0.7, 7.0, -25.3, -23.5, CD, MD, 21, CD, axis='z')
add('rig-stair-ring-w-landing', -25.3, -MX, 3.9, MD, 7.0, 8.8, '/')
add('rig-stair-ring-w-wall', -25.4, -25.3, 0, MD + RAIL, 2.8, 8.9, 'W', top=True)
rail('rig-rail-ring-w-landing', -25.4, -MX, 8.8, 8.9, MD)
add('rig-stair-ring-w-skirt', -MX, -MX + .1, 0, 3.9, 2.8, 7.0, 'W')
stair('rig-stair-ring-n', -4.5, -10.8, MZ, MZ + 1.8, CD, MD, 21, CD)
add('rig-stair-ring-n-landing', -12.6, -10.8, 3.9, MD, MZ, MZ + 1.8, '/')
add('rig-stair-ring-n-wall', -12.7, -6.6, 0, MD + RAIL, MZ + 1.8, MZ + 1.9, 'W', top=True)
rail('rig-rail-ring-n-landing', -12.7, -12.6, MZ, MZ + 1.8, MD)
add('rig-stair-ring-n-skirt', -10.8, -6.6, 0, 3.9, MZ - .1, MZ, 'W')

# ---- accommodation (team A, west; the mirrored east block is the process end, team B: same collision, different dressing).
#      Two storeys: the muster room at main deck level (3.3 m; doors N, S, E: 1.8-2.0 m wide, full height; a locker row splits it
#      into two halves joined at the west end), the cabins above as one solid block; the roof is NOT walkable (pilot column)
AX0, AX1, AZ = -MX, -17.0, 7.0
add('rig-accom-a-wall-w', AX0, AX0 + .3, MD, 7.5, -AZ, AZ, '#')
add('rig-accom-a-wall-n-0', AX0, -19.4, MD, 7.5, AZ - .3, AZ, '#'); add('rig-accom-a-wall-n-1', -17.6, AX1, MD, 7.5, AZ - .3, AZ, '#')   # doors 1.8 m
add('rig-accom-a-wall-s-0', AX0, -19.4, MD, 7.5, -AZ, -AZ + .3, '#'); add('rig-accom-a-wall-s-1', -17.6, AX1, MD, 7.5, -AZ, -AZ + .3, '#')
add('rig-accom-a-wall-e-0', AX1 - .3, AX1, MD, 7.5, -AZ, 1.0, '#'); add('rig-accom-a-wall-e-1', AX1 - .3, AX1, MD, 7.5, 3.0, AZ, '#')     # door 2 m: the module and skid catch its lines
add('rig-accom-a-lockers', -21.6, AX1 - .3, MD, MD + 2.4, -0.6, 0.6, 'O')   # locker row: no line runs door to door through the room
add('rig-accom-a-screen', -19.3, -19.0, MD, 7.5, 0.6, 3.6, '#')           # E door vestibule (1.7 m): no line from the tunnel or drill floor reaches a spawn
add('rig-accom-a-upper', AX0, AX1, 7.5, 11.0, -AZ, AZ, 'A', top=True)
add('rig-accom-a-utility', -MX, -18.5, CD, 3.9, -4.0, 4.0, 'U')       # sewage / HVAC plant under it on the cellar deck (to the deck)

# ---- the drill floor (top 8.4, 12 x 12) on its substructure, the derrick's four legs as cover, the BOP stack under it
add('rig-deck-drill', -6, 6, 8.1, DF, -6, 6, ':', mirror=False)
add('rig-derrick-leg-n-w', -4.6, -3.6, DF, 20.4, 3.6, 4.6, 'L'); add('rig-derrick-leg-n-e', 3.6, 4.6, DF, 20.4, 3.6, 4.6, 'L')
add('rig-derrick-crown', -2.0, 2.0, 20.4, 21.4, -2.0, 2.0, 'L', mirror=False)
PILOT.append(dict(id='rig-top-derrick', center=v(0, 0, 0), size=v(9.2, 0, 9.2), bottom=20.3))   # the derrick top is unstandable
add('rig-tubulars-w', -5.4, -2.8, DF, DF + 1.1, 0.4, 1.4, 'o')          # racked drill pipe on the pipe deck: half cover
rail('rig-rail-drill-n-0', -6, -1.1, 5.9, 6, DF); rail('rig-rail-drill-n-1', 1.1, 6, 5.9, 6, DF); rail('rig-rail-drill-w', -6, -5.9, -5.9, 5.9, DF)
add('rig-bop', -1.2, 1.2, MD, 8.1, -1.2, 1.2, 'B', mirror=False)          # BOP stack and riser in the substructure: full cover
# substructure windwalls under the drill floor's north and south edges: the substructure is an E-W tunnel (the centre lane)
add('rig-substructure-n-w', -6.0, -1.1, MD, 8.1, 5.7, 6.0, 'W'); add('rig-substructure-n-e', 1.1, 6.0, MD, 8.1, 5.7, 6.0, 'W')
add('rig-choke-manifold-n-w', -4.5, -2.5, MD, MD + 2.6, 3.0, 5.0, 'm')     # choke and kill manifolds: the tunnel's cover either side of the BOP
# drill stairs, main deck to drill floor, from the north and south walkways (feet in the burner pockets), walled both sides
stair('rig-stair-drill-n', 12.3, 6.0, -1.0, 1.0, MD, DF, 21, MD, axis='z')
add('rig-stair-drill-n-wall-w', -1.1, -1.0, MD, DF + RAIL, 6.0, 10.2, 'W', top=True); add('rig-stair-drill-n-wall-e', 1.0, 1.1, MD, DF + RAIL, 6.0, 10.2, 'W', top=True)

# ---- process area on the main deck: modules (3.6-3.8 m), the flare knock-out drums (3 m), a separator, crane pedestals, skids;
#      low pipe racks (1.0-1.1 m half cover) and high pipe bridges (3.2 m clear)
add('rig-module-n-w', -14.5, -8.5, MD, 8.0, 2.0, 5.4, 'M', top=True)
add('rig-module-n-e', 12.0, 17.0, MD, 7.8, 7.0, 12.0, 'M', top=True)
add('rig-separator-n-e', 8.0, 14.5, MD, 7.2, 2.0, 4.6, 'T', top=True)
add('rig-kodrum-n-w', -9.4, -4.0, MD, 7.2, 10.0, 13.0, 'K', top=True); add('rig-kodrum-n-e', 4.0, 9.4, MD, 7.2, 10.0, 13.0, 'K', top=True)
add('rig-crane-n-e', 14.5, 17.1, MD, 9.4, 14.6, 17.2, 'C', top=True)     # pedestal crane (the boom and cab are decor)
add('rig-skid-n-w', -16.5, -14.0, MD, 6.8, 15.0, 17.3, 'S', top=True)    # fire-water pump skid
add('rig-skid-n-e', 9.6, 12.0, MD, 6.8, 12.4, 15.0, 'S', top=True)       # HVAC skid: plugs the drum-module gap, staggers the walkway
add('rig-rack-low-n-w', -21.5, -17.5, MD, MD + 1.1, 13.0, 14.0, 'p'); add('rig-rack-low-n-e', 17.5, 21.5, MD, MD + 1.1, 13.0, 14.0, 'p')
add('rig-spool-a', -14.6, -13.4, MD, MD + 1.0, -1.6, -0.4, 'p')           # pipe spool in front of the muster room door
add('rig-skid-a', -11.0, -9.0, MD, 6.8, 0.2, 2.0, 'S', top=True)          # chemical injection skid: the alley jogs round it
add('rig-crate-n-e-0', 20.6, 21.8, MD, MD + 1.2, 8.2, 9.4, 'x', mat='wood', role='cover', hp=34)   # laydown: supply crates under the crane
add('rig-crate-n-e-1', 21.8, 23.0, MD, MD + 1.2, 8.2, 9.4, 'x', mat='wood', role='cover', hp=34)
add('rig-rack-high-n', -4.0, 4.0, 7.4, 8.2, 12.0, 13.0, 'h', top=True)    # flare header bridge over the walkway, drum to drum
add('rig-rack-high-w', -13.0, -11.8, 7.4, 8.2, -2.0, 2.0, 'h', top=True)  # pipe bridge over the centre alley

# ---- cellar deck plant under the main deck: two blast walls (x +-8, full height, offset doorways) split the hall into the west
#      plant room (A's basement), the wellbay (the side stairs land here) and the east plant room. Tops under the 3.9 m deck have
#      no headroom, so they need no pilot column (one would cut the deck above).
for k, (z0, z1) in enumerate(((-MZ + .2, -4.0), (-1.0, 10.0), (13.0, MZ - .1))):
    add('rig-blastwall-w-%d' % k, -8.2, -8.0, CD, 3.9, z0, z1, 'W')        # doorways z 10..13 (north aisle) and z -4..-1
add('rig-wellhead', -2.4, 2.4, CD, 2.6, -2.4, 2.4, 'X', mirror=False)     # wellbay: Xmas trees and the conductor frame
add('rig-separator-cellar-n', -6.5, 1.5, CD, 2.8, 6.5, 9.5, 'T')         # test separator (horizontal)
add('rig-sump-cellar-n', -2.0, 1.0, CD, 3.2, 12.5, 15.5, 'T'); add('rig-filter-cellar-n', -7.0, -5.0, CD, 2.6, 12.0, 14.0, 'T')
add('rig-pig-launcher-n-e', 3.0, 8.0, CD, 1.1, 4.0, 5.0, 'p')             # pig launcher barrel: half cover
add('rig-leg-n-w', -20.0, -17.6, CD, 3.9, 10.0, 12.4, 'J'); add('rig-leg-n-e', 17.6, 20.0, CD, 3.9, 10.0, 12.4, 'J')   # jacket legs
add('rig-heater-cellar-n-w', -17.0, -13.0, CD, 2.8, 2.0, 4.6, 'T')       # glycol heater (horizontal)
add('rig-manifold-cellar-n-w', -11.0, -8.2, CD, 2.4, 4.0, 6.4, 'S')       # production manifold against the blast wall
add('rig-pipes-cellar-n-w', -16.0, -10.0, CD, 1.1, 14.6, 15.6, 'p'); add('rig-pipes-cellar-n-e', 10.0, 16.0, CD, 1.1, 3.0, 4.0, 'p')
add('rig-tank-cellar-n-e', 9.5, 12.5, CD, 3.2, 10.5, 13.5, 'T')
add('rig-switchroom-n-e', 11.0, 16.0, CD, 3.9, 5.0, 8.5, 'U')            # electrical room (to the deck)
add('rig-compressor-cellar-n-e', 16.5, 19.5, CD, 2.6, 5.0, 8.4, 'S')
add('rig-pump-cellar-n-e', 15.0, 17.4, CD, 2.4, 14.0, 16.4, 'S')
# weather cladding under the main deck's edges (to the deck): the ring and the hall meet at doorways
add('rig-clad-n-w', -MX, -15.0, CD, 3.9, MZ - .2, MZ, 'W'); add('rig-clad-n-e', 15.0, MX, CD, 3.9, MZ - .2, MZ, 'W')
add('rig-clad-w-n', -MX, -MX + .2, CD, 3.9, 9.5, MZ - .2, 'W'); add('rig-clad-e-n', MX - .2, MX, CD, 3.9, 9.5, MZ - .2, 'W')
add('rig-clad-n', -2.0, 10.0, CD, 3.9, MZ - .2, MZ, 'W')                  # doorways x -15..-10.8, -6.6..-2 (stair foot), 10..15 (boat landing)
# survival-gear lockers on the open ring: two make the locker bay (FFA spawns g, G), one breaks up the end ring
add('rig-locker-ring-n-e', 2.0, 4.4, CD, 2.6, 20.2, Z1 - .1, 'O', top=True); add('rig-locker-ring-n-w', -4.4, -2.0, CD, 2.6, 20.2, Z1 - .1, 'O', top=True)
add('rig-locker-ring-w', X0 + .1, -25.8, CD, 2.6, -12.0, -9.6, 'O', top=True)
add('rig-barrels-ring-w', X0 + .1, -26.7, CD, 1.2, 12.0, 13.2, 'x', role='cover', hp=150)   # oil drums on the open ring: half cover

# ---- hazards (UnityMaps.json `hazards[]`): the flare burners use the Ward Zero burner record (NativeStageHazards: first/offset are
#      s after FightAt; even rounds add `offset`; warn, live, dwell inside the period; zones inside the volume; never lethal);
#      the blowdown vents use the Factory vent record (start s after the arm time, FightAt + 4 s)
def burner(**k):
    d = dict(id='', kind='burner', center=v(0, 0, 0), size=v(0, 0, 0), axis=v(1, 0, 0), a=v(0, 0, 0), b=v(0, 0, 0), first=0, period=0, offset=0, warn=0,
             danger=0, speed=0, length=0, charge=0, live=0, tick=0, damage=0, radius=0, start=0, rise=0, travel=0, dwell=0, hoist=0, carry=0, push=0,
             lethal=False, alternate=False, zones=[])
    d.update(k); return d
def hz(**k):
    d = dict(id='', kind='', a=v(0, 0, 0), b=v(0, 0, 0), axis=v(0, 0, 0), size=v(0, 0, 0), radius=0, start=0, period=0, offset=0,
             warn=0, rise=0, travel=0, dwell=0, hoist=0, carry=0, damage=0, tick=0, push=0, lethal=False, alternate=False, zones=[])
    d.update(k); return d
def mv(p): return v(-p['x'], p['y'], -p['z'])
BY = MD + 1.75; BZ0 = 13.0; BW = MZ - BZ0                            # burner volume 4.2..7.7 over the walkway z 13..17.5
BURN_N = burner(id='rig-burner-n', center=v(0, BY, BZ0 + BW / 2), size=v(12.0, 3.5, BW), first=6, period=10, offset=5, warn=1.5, live=2.5, tick=.25, damage=8, dwell=1.0,
                zones=[{'center': v(x, BY, BZ0 + BW / 2), 'size': v(2.0, 3.5, BW)} for x in (-4.0, 4.0)])   # two jet rows; the pocket between is safe
BURN_S = dict(BURN_N, id='rig-burner-s', center=mv(BURN_N['center']), first=11, offset=-5, zones=[{'center': mv(z['center']), 'size': z['size']} for z in BURN_N['zones']])
VENT_N = hz(id='rig-vent-blowdown-n', kind='vent', a=v(4.0, 0, 15.0), size=v(1.4, 3.2, 1.4), start=2, period=12, offset=0, warn=1.5, dwell=2.5, damage=6, tick=.5)
VENT_S = dict(VENT_N, id='rig-vent-blowdown-s', a=mv(VENT_N['a']), offset=6)
HAZ = [BURN_N, BURN_S, VENT_N, VENT_S]

# ---- spawns (x, z, floor top); yaw from +z toward +x, facing the origin as today. Team A in the muster room, B the mirror.
teamA = [(-21.9, 4.6, MD), (-21.9, -4.6, MD), (-21.0, 2.4, MD), (-19.2, -2.4, MD)]
teamB = [(-x, -z, f) for x, z, f in teamA]
ffa_names = ['A1', 'B1', 'f', 'F', 'g', 'G', 'h', 'H']
ffa = [teamA[0], teamB[0], (-11.0, -1.0, CD), (11.0, 1.0, CD), (0.8, -20.8, CD), (-0.8, 20.8, CD), (-19.0, -10.6, MD), (19.0, 10.6, MD)]
def yaw(p): return (math.degrees(math.atan2(-p[0], -p[1])) + 360) % 360

def hazard_rects():
    """2D footprints of every hazard volume (x0, x1, z0, z1, name, y0, y1): what spawns keep 3 m from."""
    out = []
    for h in HAZ:
        if h['kind'] == 'burner':
            c, s = h['center'], h['size']; out.append((c['x'] - s['x'] / 2, c['x'] + s['x'] / 2, c['z'] - s['z'] / 2, c['z'] + s['z'] / 2, h['id'], c['y'] - s['y'] / 2, c['y'] + s['y'] / 2))
        else:
            a, s = h['a'], h['size']; out.append((a['x'] - s['x'] / 2, a['x'] + s['x'] / 2, a['z'] - s['z'] / 2, a['z'] + s['z'] / 2, h['id'], a['y'], a['y'] + s['y']))
    return out
def zone_rects():
    return [(z['center']['x'] - z['size']['x'] / 2, z['center']['x'] + z['size']['x'] / 2, z['center']['z'] - z['size']['z'] / 2, z['center']['z'] + z['size']['z'] / 2)
            for h in HAZ if h['kind'] == 'burner' for z in h['zones']]

# ---- 3D sight lines: eyes 1.2/1.6/1.9 m over each spawn's own floor; a box blocks where the segment passes through it in x, y and z.
#      Boxes topping out under 2.0 m above the lower floor do not count (half cover does not hide a standing pilot); handrails never do.
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
SOLID = [e for e in [FLOOR] + els if e['ch'] != 'r']
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

# ---- 3D deck raster: 0.25 m cells (finer than a 0.3 m tread, so neighbours differ by one riser at most). A cell is standable on a
#      block top if nothing sits on that top at the cell centre, there is 1.8 m headroom and no block intrudes the 0.42 m capsule ring
#      between top+0.45 and top+1.8; neighbours connect when |dy| <= 0.25 (the 0.2 m risers) or equal. Flood fill from spawn A1.
CELL = .25; NX, NZ = int((X1 - X0) / CELL), int((Z1 - Z0) / CELL)
def raster():
    cols = {}
    for e in [FLOOR] + els:
        x0, x1, y0, y1, z0, z1 = e['box']
        for i in range(max(0, int((x0 - CAP - X0) / CELL) - 1), min(NX, int((x1 + CAP - X0) / CELL) + 2)):
            cx = X0 + (i + .5) * CELL; dx = max(x0 - cx, 0, cx - x1)
            if dx >= CAP: continue
            for j in range(max(0, int((z0 - CAP - Z0) / CELL) - 1), min(NZ, int((z1 + CAP - Z0) / CELL) + 2)):
                cz = Z0 + (j + .5) * CELL; dz = max(z0 - cz, 0, cz - z1)
                if dx * dx + dz * dz < CAP * CAP: cols.setdefault((i, j), []).append((y0, y1, x0 < cx < x1 and z0 < cz < z1))
    surf = {}
    for c, lst in cols.items():
        for y0, y1, inside in lst:
            if not inside: continue
            t = y1
            if any(inn and b0 < t + .45 and b1 > t + .01 for b0, b1, inn in lst): continue           # something sits on this top
            if any(b0 < t + 1.8 and b1 > t + .45 for b0, b1, inn in lst): continue                  # headroom / capsule ring
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
def level(t): return 'cellar' if abs(t - CD) < .05 else 'main' if abs(t - MD) < .05 else 'drill' if abs(t - DF) < .05 else 'other'
def at(surf, seen, p):
    c = (int((p[0] - X0) / CELL), int((p[1] - Z0) / CELL)); return any((c, t) in seen for t in surf.get(c, ()) if abs(t - p[2]) < .05)

# ---- longest sight lines per deck (design check: no full-map diagonal); eyes 1.6 m over walkable sample points every 2 m
def sightlines(surf, seen):
    out = {}
    for lv, y in (('cellar', CD), ('main', MD)):                          # the drill floor is 12 x 12 m: no long line fits
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
    """Each flight on its own: flood from its bottom step's cells through its own steps only; ok if the top step is reached and
    both end steps are reachable from A1 (so the flight joins its two decks)."""
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

def paint(surf, seen):
    W = X1 - X0; R = (Z1 - Z0) // 2; outs = []
    hz_rects = hazard_rects(); zones = zone_rects()
    for lv, y, ylo, yhi in (('cellar deck (top 0)', CD, -1, 3.85), ('main deck (top 4.2)', MD, 3.85, 7.35), ('drill floor (top 8.4), roofs and pipe bridges above 7.35 m', DF, 7.35, 99)):
        g = [[' '] * W for _ in range(R)]
        def put(x, z, ch):
            c = int(math.floor(x - X0)); r = int((Z1 - z) // 2)
            if 0 <= c < W and 0 <= r < R: g[r][c] = ch
        def rect(x0, x1, z0, z1, ch):
            zz = z0
            while zz < z1 - 1e-9:
                xx = x0
                while xx < x1 - 1e-9: put(xx + .05, zz + .05, ch); xx += .5
                zz += .5
        for (i, j), t in seen:
            if abs(t - y) < .05: put(X0 + (i + .5) * CELL, Z0 + (j + .5) * CELL, '.')
        for x0, x1, z0, z1, name, h0, h1 in hz_rects:
            if ylo <= h0 < yhi: rect(x0, x1, z0, z1, 'v' if 'vent' in name else '~')
        if lv.startswith('main'):
            for r in zones: rect(*r, '*')
        for e in sorted(els, key=lambda e: e['ch'] == 'r'):
            b = e['box']
            if e['ch'] in '.:' or not (ylo <= b[2] < yhi): continue
            rect(b[0], b[1], b[4], b[5], '+' if e['ch'] == 'r' else e['ch'])
        for nm, p in zip(ffa_names, ffa):
            if abs(p[2] - y) < .05: put(p[0], p[1], nm)
        for k, p in enumerate(teamA + teamB):
            if abs(p[2] - y) < .05 and p not in ffa: put(p[0], p[1], str(k + 1))
        outs.append(lv + ':\n' + ' ' * 10 + 'x=-28' + ' ' * 23 + 'x=0' + ' ' * 20 + 'x=+27')
        for r in range(R): z = Z1 - 2 * r; outs.append(f'{z-2:+4d}..{z:+3d} ' + ''.join(g[r]))
    return '\n'.join(outs)

def unity_json():
    def block(e):
        x0, x1, y0, y1, z0, z1 = e['box']
        return {'id': e['id'], 'material': e['mat'], 'pos': v((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), 'size': v(x1 - x0, y1 - y0, z1 - z0),
                'rotation': {'x': 0, 'y': 0, 'z': 0, 'w': 1}, 'role': e['role'], 'mass': 26, 'hp': e['hp']}
    sp = lambda p: {'pos': v(p[0], p[2] + 1.1, p[1]), 'yaw': round(yaw(p), 3)}
    return {'schema': 1, 'materials': [],
            'maps': [{'id': 'rig', 'name': 'Deepwell', 'tag': 'Deepwell Alpha, force nine. Hold the rail.', 'theme': 'rig',
                      'music': 'rig-deepwell', 'ambience': 'rig-storm', 'look': 'rig-storm', 'sky': 0x0B0F16, 'floor': 0x4A5058, 'accent': 0xFF7A1A,
                      'rotation': False, 'bounds': BOUNDS, 'spawns': {'ffa': [sp(p) for p in ffa], 'teams': [{'side': 0, 'points': [sp(p) for p in teamA]}, {'side': 1, 'points': [sp(p) for p in teamB]}]},
                      'blocks': [block(FLOOR)] + [block(e) for e in els], 'segments': [], 'pilotBlocks': PILOT, 'hazards': HAZ,
                      'killVolumes': [],          # the sea: killY -6 covers every drop; presentation draws the water at y -14
                      'nav': {'nodes': [], 'links': []}}]}

def png(path, surf, seen, scale=8):
    PW, GAP = (X1 - X0) * scale, 16; PH = (Z1 - Z0) * scale; W, H = 3 * PW + 2 * GAP, PH; img = bytearray(b'\x0c\x18\x28' * W * H)
    def fill(k, x0, x1, z0, z1, c, alpha=1.0):
        ox = k * (PW + GAP); i0, i1 = int((x0 - X0) * scale), int(math.ceil((x1 - X0) * scale)); j0, j1 = int((Z1 - z1) * scale), int(math.ceil((Z1 - z0) * scale))
        for j in range(max(0, j0), min(H, j1)):
            for i in range(max(0, i0), min(PW, i1)):
                o = (j * W + ox + i) * 3
                for q in range(3): img[o + q] = int(img[o + q] * (1 - alpha) + c[q] * alpha)
    for k in range(3): fill(k, X0, X1, Z0, Z1, (14, 30, 48))
    floor = {CD: (0, (74, 78, 84)), MD: (1, (96, 100, 106)), DF: (2, (124, 112, 92))}
    for (i, j), t in surf.items():
        for u in t:
            for y, (k, c) in floor.items():
                if abs(u - y) < .05: fill(k, X0 + i * CELL, X0 + (i + 1) * CELL, Z0 + j * CELL, Z0 + (j + 1) * CELL, c if ((i, j), u) in seen else (120, 50, 50))
    for x0, x1, z0, z1 in [(-MX, MX, -MZ, MZ)]: fill(0, x0, x1, z0, z1, (0, 0, 0), .25)    # cellar under the main deck: covered
    for x0, x1, z0, z1 in HOLES: fill(0, x0, x1, z0, z1, (255, 255, 255), .12)
    col = {'#': (150, 140, 120), 'A': (170, 150, 110), 'U': (90, 90, 100), 'L': (220, 120, 40), 'B': (200, 60, 60), 'o': (150, 130, 100), 'W': (130, 135, 145),
           'M': (70, 110, 95), 'T': (95, 120, 140), 'K': (200, 140, 60), 'C': (230, 200, 40), 'S': (110, 95, 125), 'p': (160, 150, 120), 'h': (120, 100, 80),
           'X': (180, 70, 60), 'J': (80, 85, 95), '/': (185, 185, 190), '!': (255, 230, 0), 'r': (250, 210, 60)}
    for e in sorted(els, key=lambda e: (e['ch'] == 'r', e['box'][3])):
        b = e['box']
        if e['ch'] in '.:': continue
        k = 0 if b[2] < 3.85 else 1 if b[2] < 7.35 else 2
        fill(k, b[0], b[1], b[4], b[5], col.get(e['ch'], (128, 128, 128)), .6 if e['ch'] == 'h' else 1.0)
    fill(2, -MX, MX, -MZ, MZ, (96, 100, 106), .25)                       # third panel: the main deck's outline under the overhead
    for p in PILOT:                                                        # pilot-only columns (magenta): rails and unstandable tops
        c, z = p['center'], p['size']; fill(2, c['x'] - z['x'] / 2 - .05, c['x'] + z['x'] / 2 + .05, c['z'] - z['z'] / 2 - .05, c['z'] + z['z'] / 2 + .05, (230, 60, 200), .45)
    for x0, x1, z0, z1, name, h0, h1 in hazard_rects():
        fill(0 if h0 < 3.85 else 1, x0, x1, z0, z1, (180, 210, 240) if 'vent' in name else (255, 120, 20), .35)
    for r in zone_rects(): fill(1, *r, (255, 80, 0), .5)
    for idx, p in enumerate(teamA + teamB):
        k = 0 if p[2] < 3.85 else 1; fill(k, p[0] - .6, p[0] + .6, p[1] - .6, p[1] + .6, (60, 200, 255) if idx < 4 else (255, 90, 120))
    for p in ffa[2:]:
        k = 0 if p[2] < 3.85 else 1; fill(k, p[0] - .5, p[0] + .5, p[1] - .5, p[1] + .5, (245, 245, 245))
    raw = b''.join(b'\x00' + bytes(img[j * W * 3:(j + 1) * W * 3]) for j in range(H))
    ch = lambda t, d: struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    with open(path, 'wb') as f: f.write(b'\x89PNG\r\n\x1a\n' + ch(b'IHDR', struct.pack('>IIBBBBB', W, H, 8, 2, 0, 0, 0)) + ch(b'IDAT', zlib.compress(raw, 9)) + ch(b'IEND', b''))

if __name__ == '__main__':
    surf, seen = raster()
    print(paint(surf, seen))
    print('legend: . walkable (reached from A1)  + handrail  / stair, landing  W wall, cladding, stair tower  # muster room wall  O lockers  A roof (unwalkable)'
          '\n        M module  T vessel  K knock-out drum  S skid, pump  C crane pedestal  U utility, switch room  J jacket leg  X wellhead  B BOP'
          '\n        m manifold  L derrick  o tubulars  p pipes (half cover)  x crate/drums (breakable)  h pipe bridge  ! telltale  ~ burner volume  * jet row'
          '\n        v vent  A1 B1 f F g G h H FFA spawns, 1-8 team spawns; the sea notches are the gaps in the cellar edge rail')
    named = list(zip(ffa_names, ffa)); bad = check(list(itertools.combinations(named, 2)), 'FFA')
    A = [(f'A{i+1}', p) for i, p in enumerate(teamA)]; B = [(f'B{i+1}', p) for i, p in enumerate(teamB)]; bad += check([(p, q) for p in A for q in B], 'Teams A-B')
    d3 = lambda p, q: math.dist((p[0], p[2], p[1]), (q[0], q[2], q[1]))
    print('min FFA spacing %.1f m (plan; %.1f m 3D); min A-B spacing %.1f m' % (min(math.dist(p[:2], q[:2]) for p, q in itertools.combinations(ffa, 2)),
          min(d3(p, q) for p, q in itertools.combinations(ffa, 2)), min(math.dist(p[:2], q[:2]) for p in teamA for q in teamB)))
    sp = ffa + teamA[1:] + teamB[1:]
    worst = min((dist_rect(p, r[:4]), r[4], p[:2]) for p in sp for r in hazard_rects())
    print('closest spawn to any hazard volume (plan footprint): %.1f m (%s, spawn %s)' % worst); bad += worst[0] < 3
    worst = min((dist_rect(p, n[1:]), n[0], p[:2]) for p in sp for n in NOTCHES)
    print('closest spawn to a sea notch (boat landing / lifeboat station): %.1f m (%s, spawn %s)' % worst); bad += worst[0] < 8
    clr = min((min(dist_rect(p[:2], (e['box'][0], e['box'][1], e['box'][4], e['box'][5])) for e in els if e['box'][2] < p[2] + 1.9 and e['box'][3] > p[2] + .3), p[:2]) for p in sp)
    print('closest spawn to any block at its floor: %.2f m at %s' % clr); bad += clr[0] < CAP
    inb = all(abs(p[0]) <= 28 - CAP and abs(p[1]) <= 22 - CAP for p in sp); print('all spawns inside the bounds minus the capsule:', inb); bad += not inb
    lv = {}
    for c, ts in surf.items():
        for t in ts: k = level(t); lv.setdefault(k, [0, 0]); lv[k][1] += 1; lv[k][0] += (c, t) in seen
    print('deck raster (0.25 m cells) reachable on foot from A1: ' + '; '.join('%s %d of %d' % (k, lv[k][0], lv[k][1]) for k in ('cellar', 'main', 'drill', 'other') if k in lv) + ' (other: stair treads, half-cover tops; the rest are walled or capped tops)')
    reach = {n: at(surf, seen, p) for n, p in list(zip(ffa_names, ffa)) + A + B}
    print('every spawn reachable:', all(reach.values()), '' if all(reach.values()) else [n for n, r in reach.items() if not r]); bad += not all(reach.values())
    decks = all(lv.get(k, [0])[0] > 0 for k in ('cellar', 'main', 'drill')); print('every deck reachable:', decks); bad += not decks
    for k, ((d, p, q), (dd, pp, qq)) in sightlines(surf, seen).items():
        print('longest clear sightline, %s deck (eyes 1.6 m, 2 m sample grid): %.1f m %s-%s; longest diagonal (|dx|,|dz| >= 14, fails at 44 m): %s' % (k, d, p, q, '%.1f m %s-%s' % (dd, pp, qq) if pp else 'none of 30 m or more'))
        bad += dd >= 44
    fl = flights(surf, seen); okf = all(r[2] for r in fl)
    print('stairs: %d flights, each walkable bottom to top on its own and joined to both decks: %s (%s); 21 risers of 0.20 m x 0.30 m, 1.8-2.0 m wide'
          % (len(fl), okf, ', '.join('%s %s' % (n.replace('rig-stair-', ''), 'ok' if r else 'BROKEN') for n, k, r in fl))); bad += not okf or len(fl) != 6
    key = lambda b: tuple(round(c, 3) for c in b)
    sym = sorted(key(e['box']) for e in [FLOOR] + els) == sorted(key(mbox(e['box'])) for e in [FLOOR] + els)
    psym = sorted((round(p['center']['x'], 3), round(p['center']['z'], 3), round(p['size']['x'], 3), round(p['size']['z'], 3), p['bottom']) for p in PILOT) == \
           sorted((round(-p['center']['x'], 3), round(-p['center']['z'], 3), round(p['size']['x'], 3), round(p['size']['z'], 3), p['bottom']) for p in PILOT)
    ssym = sorted(teamB) == sorted((-x, -z, f) for x, z, f in teamA) and sorted(ffa) == sorted((-x, -z, f) for x, z, f in ffa)
    print('point symmetric (x,z)->(-x,-z): blocks %s, pilot blocks %s, spawns %s' % (sym, psym, ssym)); bad += not (sym and psym and ssym)
    print('pilot blocks: %d (%d over handrails, the rest over unstandable tops: accommodation roofs, modules, drums, cranes, skids, walls, derrick)' % (len(PILOT), sum(1 for e in els if e['ch'] == 'r')))
    # hazard fairness numbers
    b = BURN_N; cross = (b['zones'][0]['size']['x'] + 2 * CAP) / 6.8
    print('flare burners: N first %.0f s (even rounds +%.0f), S first %.0f s (even rounds %.0f): one fires every %.0f s, alternating; telegraph %.1f s, live %.1f s, dwell %.1f s, period %.0f s'
          % (b['first'], b['offset'], BURN_S['first'], BURN_S['offset'], b['period'] / 2, b['warn'], b['live'], b['dwell'], b['period']))
    zx = sorted(z['center']['x'] for z in b['zones']); zw = b['zones'][0]['size']['x']
    print('   %d jet rows of %.0f m across the %.1f m walkway, a %.0f m safe pocket between; crossing a live row at 6.8 m/s = %.2f s = %d ticks x %d = %d; standing a whole window = %d (non-lethal ticks)'
          % (len(zx), zw, BW, zx[1] - zx[0] - zw, cross, math.ceil(cross / b['tick']), b['damage'], math.ceil(cross / b['tick']) * b['damage'], int(b['live'] / b['tick']) * b['damage']))
    vt = VENT_N; print('blowdown vents (cellar): every %.0f s, offset %.0f s apart, hiss %.1f s, burst %.1f s, %d per %.1f s tick (%d per burst)'
          % (vt['period'], VENT_S['offset'], vt['warn'], vt['dwell'], vt['damage'], vt['tick'], int(vt['dwell'] / vt['tick']) * vt['damage']))
    okz = all(abs(z['center'][k] - h['center'][k]) + z['size'][k] / 2 <= h['size'][k] / 2 + 1e-3 for h in HAZ if h['kind'] == 'burner' for z in h['zones'] for k in 'xyz')
    okt = all(h['first'] >= 4 and h['first'] + h['offset'] >= 4 and h['warn'] >= 1.5 and h['warn'] + h['live'] + h['dwell'] <= h['period'] and not h['lethal'] for h in HAZ if h['kind'] == 'burner')
    okv = all(h['warn'] >= 1.5 and h['warn'] + h['dwell'] <= h['period'] and h['a']['y'] + h['size']['y'] < 3.9 for h in HAZ if h['kind'] == 'vent')
    print('burner zones inside their volumes: %s; burner clock (idle >= 4 s, warn >= 1.5 s, fits the period, non-lethal): %s; vents (warn >= 1.5 s, under the deck): %s' % (okz, okt, okv)); bad += not (okz and okt and okv)
    ids = [e['id'] for e in [FLOOR] + els]; dup = len(ids) - len(set(ids)); print('duplicate block ids: %d' % dup); bad += dup > 0
    print('blocks: %d (+ floor); hazards: %d; pilot blocks: %d; spawns: %d FFA, %d + %d team' % (len(els), len(HAZ), len(PILOT), len(ffa), len(teamA), len(teamB)))
    bad += len(els) + 1 > 350
    if '--json' in sys.argv:
        with open(sys.argv[sys.argv.index('--json') + 1], 'w') as f: json.dump(unity_json(), f, separators=(',', ':'))
    if '--out' in sys.argv:
        out = sys.argv[sys.argv.index('--out') + 1]; os.makedirs(out, exist_ok=True)
        with open(os.path.join(out, 'rig_m1.json'), 'w') as f: json.dump(unity_json(), f, separators=(',', ':'))
        png(os.path.join(out, 'rig_m1.png'), surf, seen); print('wrote', os.path.join(out, 'rig_m1.json'), 'and rig_m1.png')
    print('checks:', 'PASS' if not bad else 'FAIL (%d)' % bad)
    sys.exit(1 if bad else 0)
