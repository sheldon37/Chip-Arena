"""Deepwell's stage art (docs/arenas/DEEPWELL.md "Stage art", docs/arenas/STAGE-ART.md): Deepwell Station modelled on the map's
blocks, the seabed round it (rig_sea.py), its rig and its breakable cases (rig_props.py), into the bank ChipArenaStageDeepwell.

  blender --background --factory-startup --python tools/stages/rig_art.py -- --layout LAYOUT.json --out Assets/LocalAssets
  (LAYOUT.json: dotnet run --project Tests/CoreTests.csproj -c Release -- --stage-layout rig LAYOUT.json;
   --preview DIR [--views a,b] [--preview-tier N] renders the baked stage; --tiers, --no-ao, --no-sea, --static-only, --stats)

An original design: a research station on the ocean floor in riveted steel, brass and thick glass. The glass itself is not
modelled (the stage shader is opaque): every pane is an open frame the effects glaze (Core NativeDeepwellFx), so the sea shows
through. What is modelled, by block id (the art lies within .25 m of its block below 2 m over a floor; higher is overhead):
decks of plate with their inlays (the wellhead's ring, grate and warning stripes, the pools' rims); ribbed steel walls with
rivets, brass bands, portholes and lamps; pressure-door frames; the glass walls' sills, mullions and transoms; the Well's dome
of ribs and rings on its ring beam, the drill's housing and gantry on the four pylons; barrel ribs and purlins over the
tunnels, the spine, the wings and the bays; pendant and flush lamps; the bays' gantry cranes; rails of glass in brass; stairs
with brass nosings; consoles, equipment racks, lockers, decompression chambers and the survey subs in the pools.
"""
import argparse
import math
import os
import sys
import time

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import stagekit as sk  # noqa: E402
from stagekit import Geo, box, lathe, cylinder, tube  # noqa: E402
import rig_kit as rk  # noqa: E402
from rig_kit import F, ext, Run, run_of, rod, rbox, rivets, disc, dome, skin, ibeam, Y  # noqa: E402

BANK = 'ChipArenaStageDeepwell'
FLOORS = (-1.6, 0.0, 3.2)
STATS = {}


def prism_run(g, R, u0, u1, sec, f, caps=True):
    """A convex section [(d, y)] extruded along run R from u0 to u1 (flat faces)."""
    cd = sum(p[0] for p in sec) / len(sec)
    cy = sum(p[1] for p in sec) / len(sec)
    n = len(sec)
    for i in range(n):
        a, b = sec[i], sec[(i + 1) % n]
        ed, ey = b[0] - a[0], b[1] - a[1]
        nd, ny = ey, -ed
        if nd * ((a[0] + b[0]) / 2 - cd) + ny * ((a[1] + b[1]) / 2 - cy) < 0:
            nd, ny = -nd, -ny
        nrm = (R.n[0] * nd, ny, R.n[1] * nd)
        g.face((R.at(u0, a[1], a[0]), R.at(u1, a[1], a[0]), R.at(u1, b[1], b[0]), R.at(u0, b[1], b[0])), f, nrm)
    if caps:
        g.face([R.at(u0, y, d) for d, y in sec], f, R.U(-1))
        g.face([R.at(u1, y, d) for d, y in sec], f, R.U(1))


# ---------------------------------------------------------------------------------------------------------------------------
# Decks
# ---------------------------------------------------------------------------------------------------------------------------
def deck(g, b, T):
    x0, x1, y0, y1, z0, z1 = ext(b)
    i = b['id']
    top = F['bay'] if y1 < -1 else (F['deck'] if y1 < 1 else (F['teak'] if i.startswith('rig-wing-') else F['deck2']))
    thin = y1 - y0 < .5
    box(g, x0, x1, y0, y1, z0, z1, top, T, 0, skip=('-x', '+x', '-y', '-z', '+z'))
    box(g, x0, x1, y0, y1, z0, z1, F['plate'] if thin else F['hull'], T, 0, cell=0 if T.i else 3.0, skip=('+y',))
    if thin and T.detail and (x1 - x0) > 1.2 and (z1 - z0) > 1.2:
        # joists under an upper deck, across its short side
        if (x1 - x0) >= (z1 - z0):
            u = x0 + 1.0
            while u < x1 - .3:
                box(g, u - .06, u + .06, y0 - .14, y0, z0 + .05, z1 - .05, F['steel'], T, .01, cell=0, skip=('+y',))
                u += 2.0
        else:
            u = z0 + 1.0
            while u < z1 - .3:
                box(g, x0 + .05, x1 - .05, y0 - .14, y0, u - .06, u + .06, F['steel'], T, .01, cell=0, skip=('+y',))
                u += 2.0
    if i == 'rig-well-floor':
        wellhead(g, T)
    if i.startswith('rig-bay-'):
        sz = 1 if b['pos'][2] > 0 else -1
        px0, px1 = rk.POOL[0], rk.POOL[1]
        pz0, pz1 = (rk.POOL[2], rk.POOL[3]) if sz > 0 else (-rk.POOL[3], -rk.POOL[2])
        w = .35
        for (a0, a1, c0, c1) in ((px0 - w, px1 + w, pz0 - w, pz0), (px0 - w, px1 + w, pz1, pz1 + w), (px0 - w, px0, pz0, pz1), (px1, px1 + w, pz0, pz1)):
            rk.stripes(g, max(a0, x0), min(a1, x1), max(c0, z0), min(c1, z1), y1, T)


def wellhead(g, T):
    """The wellhead in the Well's floor: warning stripes round the ram's footprint, a brass ring with bolts, the grate over the core."""
    r, w = 2.2, .3
    rk.stripes(g, -r - w, r + w, r, r + w, 0, T)
    rk.stripes(g, -r - w, r + w, -r - w, -r, 0, T)
    rk.stripes(g, r, r + w, -r, r, 0, T)
    rk.stripes(g, -r - w, -r, -r, r, 0, T)
    n = (48, 24, 12)[T.i]
    rk.flat_ring(g, 0, 0, 3.3, 3.55, 0, F['brass'], n)
    rk.flat_ring(g, 0, 0, 3.75, 3.8, 0, F['brass'], n)
    if T.tiny:
        rivets(g, [(math.sin(a) * 3.42, .006, math.cos(a) * 3.42) for a in [k * math.pi / 16 for k in range(32)]], Y, .03, F['brass'])
    # the grate: a dark pit plate, a brass rim, bars across (the rig's core glows between them)
    g.face(((-1.5, 0, -1.5), (1.5, 0, -1.5), (1.5, 0, 1.5), (-1.5, 0, 1.5)), F['black'], Y)
    for (a, c, e, h) in ((-1.62, 1.62, -1.62, -1.5), (-1.62, 1.62, 1.5, 1.62), (-1.62, -1.5, -1.5, 1.5), (1.5, 1.62, -1.5, 1.5)):
        box(g, a, c, 0, .045, e, h, F['brass'], T, .01, cell=0, skip=('-y',))
    bars = (13, 7, 4)[T.i]
    for k in range(bars):
        x = -1.5 + 3.0 * (k + .5) / bars
        box(g, x - .035, x + .035, .02, .05, -1.5, 1.5, F['dark'], T, 0, cell=0, skip=('-y',))
    # the four quadrant marks pointing in at the wellhead
    for k in range(4):
        a = k * math.pi / 2 + math.pi / 4
        c, s = math.cos(a), math.sin(a)
        p = lambda d, o: (s * d + c * o, 0, c * d - s * o)  # noqa: E731
        g.face((p(4.2, 0), p(5.0, .35), p(5.0, -.35)), F['yellow'], Y)


# ---------------------------------------------------------------------------------------------------------------------------
# Walls, doors, glass
# ---------------------------------------------------------------------------------------------------------------------------
def wall(g, b, T, seed):
    i = b['id']
    x0, x1, y0, y1, z0, z1 = ext(b)
    R, h = run_of(b)
    if i.endswith('-head') or i.endswith('-sill'):
        box(g, x0, x1, y0, y1, z0, z1, F['plate'], T, .012, cell=0)
        bottom = 3.2 if y0 > 5 else (-1.6 if y0 < 2 else 0.0)
        rk.door_frame(g, R, 0, R.len, bottom, y0, h, T, stripe=i.startswith('rig-bulkhead-'))
        if y1 - y0 > 1.2:
            for side in (-1, 1):
                R.box(g, .02, R.len - .02, y0 + .3, y0 + .4, side * h, side * (h + .022), F['brass'], T, .008)
                # the door's sign: a lit strip over it
                R.box(g, R.len * .25, R.len * .75, y0 + .55, y0 + .7, side * h, side * (h + .03), F['cyandim'], None, 0)
        return
    paint = F['cream'] if '-wing-' in i else (F['seafoam'] if '-bay-' in i else None)
    rk.plate_wall(g, b, T, seed, lamps=not i.startswith('rig-bulkhead-'), ports=not i.startswith('rig-bulkhead-'), paint=paint)


def pylon(g, b, T):
    x0, x1, y0, y1, z0, z1 = ext(b)
    cx, cz, H = (x0 + x1) / 2, (z0 + z1) / 2, y1 - y0
    ph = math.pi / 8
    lathe(g, [(.75, 0), (.75, .28), (.67, .36), (.6, .5), (.6, H - .52), (.68, H - .38), (.76, H - .22), (.76, H)], (cx, y0, cz), F['steel'], 8, smooth=False, phase=ph, cap1=True)
    for y in (.9, 3.5, 6.2):
        lathe(g, [(.6, y), (.655, y + .03), (.655, y + .17), (.6, y + .2)], (cx, y0, cz), F['brass'], 8, smooth=False, phase=ph)
    d = .6 * math.cos(math.pi / 8)
    for (a, c) in ((1.3, 2.9), (4.2, 5.9)):
        for (nx, nz) in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            px, pz = cx + nx * d, cz + nz * d
            box(g, px - (.012 if nx else .06), px + (.012 if nx else .06), y0 + a, y0 + c, pz - (.012 if nz else .06), pz + (.012 if nz else .06), F['cyan'], None, 0, cell=0)
    if T.tiny:
        for y in (.33, H - .3):
            rivets(g, [(cx + math.cos(k * math.pi / 4) * .72, y0 + y, cz + math.sin(k * math.pi / 4) * .72) for k in range(8)], Y, .02, F['brass'])


def flights(L):
    out = {}
    for b in L['blocks']:
        if b['id'].startswith('rig-stair-'):
            out.setdefault(b['id'].rsplit('-', 1)[0], []).append(b)
    return out


def step(g, b, T, low):
    """A stair's step: plate tread, steel riser, a round brass nosing on the edge toward the lower step (`low`: unit x, z)."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    box(g, x0, x1, y0, y1, z0, z1, F['deck2'], T, 0, cell=0, skip=('-x', '+x', '-y', '-z', '+z'))
    box(g, x0, x1, y0, y1, z0, z1, F['steel'], T, 0, cell=0, skip=('+y', '-y'))
    sides = (6, 4, 3)[T.i]
    if abs(low[0]) > .5:
        x = x1 if low[0] > 0 else x0
        rod(g, (x, y1 - .02, z0 + .01), (x, y1 - .02, z1 - .01), .028, F['brass'], sides, caps=False)
    else:
        z = z1 if low[1] > 0 else z0
        rod(g, (x0 + .01, y1 - .02, z), (x1 - .01, y1 - .02, z), .028, F['brass'], sides, caps=False)


# ---------------------------------------------------------------------------------------------------------------------------
# Fittings
# ---------------------------------------------------------------------------------------------------------------------------
def console(g, b, T, seed):
    """A console: a dark plinth, a ridged desk with a lit screen on each slope, a brass ridge and kick rail."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    R, h = run_of(b)
    L = R.len
    R.box(g, .03, L - .03, y0, y0 + .7, -h + .05, h - .05, F['dark'], T, .02)
    prism_run(g, R, 0, L, [(-h, y0 + .7), (h, y0 + .7), (h, y0 + .84), (0, y1 - .02), (-h, y0 + .84)], F['steel'])
    sl = math.hypot(h, y1 - .02 - y0 - .84)
    for side in (-1, 1):
        # the slope's face: from (side*h, .84) up to (0, top); a screen inset in it, lifted a hair along its normal
        ny, nd = h / sl, side * (y1 - .02 - y0 - .84) / sl
        nrm = (R.n[0] * nd, ny, R.n[1] * nd)
        n = max(1, int(L / 1.3))
        for k in range(n):
            a, c = .1 + (L - .2) * k / n + .04, .1 + (L - .2) * (k + 1) / n - .04
            pts = []
            for (u, t) in ((a, .14), (c, .14), (c, .86), (a, .86)):
                d, y = side * h * (1 - t), y0 + .84 + (y1 - .02 - y0 - .84) * t
                p = R.at(u, y, d)
                pts.append((p[0] + nrm[0] * .008, p[1] + nrm[1] * .008, p[2] + nrm[2] * .008))
            g.face(pts, F['screen'] if (k + seed) % 3 else F['amber'], nrm)
        R.box(g, .02, L - .02, y0 + .1, y0 + .16, side * (h - .05), side * (h - .02), F['brass'], T, .008)
    rod(g, R.at(0.02, y1 - .02, 0), R.at(L - .02, y1 - .02, 0), .03, F['brass'], (6, 5, 4)[T.i])


def rack(g, b, T, seed):
    """An equipment rack (a bulkhead screen): cabinets with panel doors, louvres and status lights, a cable tray along its top."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    R, h = run_of(b)
    L = R.len
    R.box(g, 0, L, y0, y1 - .16, -h + .04, h - .04, F['steel'], T, .02)
    R.box(g, .05, L - .05, y1 - .16, y1 - .04, -h + .1, h - .1, F['dark'], T, .015)
    R.box(g, 0, L, y1 - .04, y1, -h, h, F['brass'], T, .012)
    n = max(1, int(round(L / .85)))
    for side in (-1, 1):
        for k in range(n):
            a, c = L * k / n + .03, L * (k + 1) / n - .03
            R.box(g, a, c, y0 + .12, y1 - .24, side * (h - .04), side * h, F['plate'], T, .012)
            if T.i < 2:
                v = int(rk.hashf(seed, k, side + 2) * 3)
                for s in range(4):
                    R.box(g, a + .1, c - .1, y0 + .3 + s * .09, y0 + .34 + s * .09, side * h, side * (h + .012), F['dark'], None, 0)
                R.box(g, a + .1, c - .1, y0 + 1.5, y0 + 1.58, side * h, side * (h + .012), (F['cyan'], F['amber'], F['cyandim'])[v], None, 0)
                if T.detail:
                    R.box(g, c - .1, c - .07, y0 + .95, y0 + 1.2, side * h, side * (h + .035), F['brass'], T, .008)
                    for s in range(3):
                        R.box(g, a + .12 + s * .1, a + .17 + s * .1, y0 + 1.72, y0 + 1.77, side * h, side * (h + .012), (F['cyan'], F['red'], F['amber'])[(s + v) % 3], None, 0)
    R.box(g, 0, L, y0, y0 + .1, -h, h, F['dark'], T, .01)


def lockers(g, b, T, seed):
    x0, x1, y0, y1, z0, z1 = ext(b)
    R, h = run_of(b)
    L = R.len
    R.box(g, 0, L, y0 + .08, y1, -h + .03, h - .03, F['cream'], T, .02)
    R.box(g, 0, L, y0, y0 + .08, -h, h, F['dark'], T, .01)
    R.box(g, 0, L, y1 - .05, y1, -h, h, F['brass'], T, .012)
    n = max(1, int(round(L / .6)))
    for side in (-1, 1):
        for k in range(n):
            a, c = L * k / n + .02, L * (k + 1) / n - .02
            R.box(g, a, c, y0 + .14, y1 - .1, side * (h - .03), side * h, F['cream'], T, .012)
            if T.i < 2:
                for s in range(3):
                    R.box(g, a + .12, c - .12, y1 - .42 + s * .07, y1 - .39 + s * .07, side * h, side * (h + .01), F['dark'], None, 0)
                R.box(g, c - .1, c - .07, y0 + 1.1, y0 + 1.3, side * h, side * (h + .03), F['brass'], T, .006)
                R.box(g, a + .16, c - .16, y0 + 1.6, y0 + 1.68, side * h, side * (h + .008), F['orange'], None, 0)


def chamber(g, b, T):
    """A decompression chamber: a cream pressure vessel on saddles, brass bands and portholes, a wheel hatch, gas bottles on top."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    R, h = run_of(b)
    L = R.len
    r = min(h - .02, .78)
    cy = y0 + .42 + r
    sides = (16, 10, 6)[T.i]
    ax = R.U()
    for u in (L * .25, L * .75):
        R.box(g, u - .12, u + .12, y0, cy - r * .5, -h + .08, h - .08, F['dark'], T, .02)
    a, c = R.at(.5, cy, 0), R.at(L - .5, cy, 0)
    cylinder(g, a, c, r, F['cream'], sides, caps=False)
    dome(g, a, r, F['cream'], sides, rings=(4, 3, 2)[T.i], axis=R.U(-1), squash=.6)
    dome(g, c, r, F['cream'], sides, rings=(4, 3, 2)[T.i], axis=ax, squash=.6)
    for u in (.62, L / 2, L - .62):
        lathe(g, [(r, -.05), (r + .03, -.035), (r + .03, .035), (r, .05)], R.at(u, cy, 0), F['brass'], sides, axis=ax)
    if T.i < 2:
        for side in (-1, 1):
            rk.porthole(g, R.at(L * .5 + .45, cy + .12, side * (r - .02)), R.N(side), .2, T)
        # the hatch wheel on the far end
        e = R.at(L - .5 + r * .6, cy, 0)
        tube(g, [sk._add(sk._add(e, R.N(), .22 * math.cos(k * math.pi / 6)), Y, .22 * math.sin(k * math.pi / 6)) for k in range(12)], .022, F['brass'], 4, closed=True)
        rod(g, sk._add(e, ax, -.12), sk._add(e, ax, .02), .035, F['brass'], 6)
    # the gas bottles and the manifold on top, up to the block's top
    for d in (-.3, 0, .3):
        p0, p1 = R.at(.45, cy + r + .16, d), R.at(L - .7, cy + r + .16, d)
        cylinder(g, p0, p1, .13, F['yellow'] if d else F['copper'], (10, 7, 5)[T.i], caps=False)
        dome(g, p1, .13, F['yellow'] if d else F['copper'], (10, 7, 5)[T.i], rings=2, axis=ax)
        dome(g, p0, .13, F['yellow'] if d else F['copper'], (10, 7, 5)[T.i], rings=2, axis=R.U(-1))
    R.box(g, .3, L - .3, cy + r + .29, min(y1, cy + r + .36), -.42, .42, F['dark'], T, .012)


def sub(g, b, T):
    """The survey sub moored in the pool: a round yellow hull with a flat deck to stand on, a glass nose, thrusters and lamps."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    R, h = run_of(b)
    L = R.len
    cy = (y0 + y1) / 2 - .05
    ry = (y1 - .1 - cy)
    n = (16, 10, 8)[T.i]
    st = (9, 6, 4)[T.i]
    rings = []
    for k in range(st + 1):
        t = -1 + 2 * k / st
        s = max(.05, (1 - abs(t) ** 2.6)) ** .5
        c = R.at(L / 2 + t * (L / 2 - .04), cy, 0)
        ringp = []
        for j in range(n):
            a = 2 * math.pi * j / n
            ca, sa = math.cos(a), math.sin(a)
            e = .62
            dd = (h - .03) * s * (abs(ca) ** e) * (1 if ca >= 0 else -1)
            yy = ry * s * (abs(sa) ** e) * (1 if sa >= 0 else -1)
            ringp.append((c[0] + R.n[0] * dd, c[1] + yy, c[2] + R.n[1] * dd))
        rings.append(ringp)
    skin(g, rings, F['yellow'])
    # the deck, its hatch, the sail's stub
    R.box(g, L * .16, L * .84, y1 - .09, y1, -h * .62, h * .62, F['dark'], T, .02)
    lathe(g, [(.34, 0), (.36, .012), (.3, .02), (.001, .02)], R.at(L * .5, y1, 0), F['brass'], (12, 8, 6)[T.i])
    R.box(g, .1, L - .1, cy - .06, cy + .06, -h, h, F['black'], T, .02)
    if T.i < 2:
        # the nose's glass and its lamps, the stern's thrusters
        dome(g, R.at(L - .16, cy + .1, 0), .42, F['lens'], (10, 7, 5)[T.i], rings=3, axis=R.U(1), squash=.5)
        lathe(g, [(.44, -.03), (.47, 0), (.44, .03)], R.at(L - .17, cy + .1, 0), F['brass'], (10, 7, 5)[T.i], axis=R.U(1))
        for side in (-1, 1):
            p = R.at(L - .3, cy - .25, side * (h * .55))
            disc(g, sk._add(p, R.U(), .13), .09, F['lamp'], 8, R.U(1))
            cylinder(g, p, sk._add(p, R.U(), .13), .1, F['dark'], 8, caps=False)
            q = R.at(.34, cy, side * (h - .16))
            cylinder(g, sk._add(q, R.U(), -.22), sk._add(q, R.U(), .16), .17, F['dark'], (10, 7, 5)[T.i], caps=True)
        R.box(g, L * .3, L * .7, y1 - .5, y1 - .38, -h + .01, -h + .03, F['black'], None, 0)


def catwalk(g, b, T):
    x0, x1, y0, y1, z0, z1 = ext(b)
    deck(g, b, T)
    for x in (x0 + .06, x1 - .06):
        ibeam(g, [(x, y0 - .16, z0), (x, y0 - .16, z1)], .3, .14, F['steel'], up=Y)
    if T.detail:
        z = z0 + 1.2
        while z < z1:
            box(g, x0 + .1, x1 - .1, y0 - .26, y0 - .16, z - .05, z + .05, F['dark'], T, .01, cell=0)
            z += 2.4


# ---------------------------------------------------------------------------------------------------------------------------
# Overhead: roofs, the dome, the drill, lamps, cranes
# ---------------------------------------------------------------------------------------------------------------------------
def barrel(g, x0, x1, z0, z1, y, rise, along_x, T, every=2.4, r=.055, purlins=3, power=1.0):
    """A glass barrel roof's bones over a room x0..x1, z0..z1 from wall-top height y: ribs across its short span, purlins along it."""
    if along_x:
        n = max(1, int(round((x1 - x0) / every)))
        for k in range(n + 1):
            x = x0 + .08 + (x1 - x0 - .16) * k / n
            rk.rib(g, (x, y, z0), (x, y, z1), rise, r, F['frame'], T, power=power)
        for k in range(purlins):
            t = (k + 1) / (purlins + 1)
            z, yy = z0 + (z1 - z0) * t, y + rise * math.sin(math.pi * t) ** power
            rod(g, (x0, yy, z), (x1, yy, z), r * .7, F['frame'], (5, 4, 3)[T.i], caps=False)
    else:
        n = max(1, int(round((z1 - z0) / every)))
        for k in range(n + 1):
            z = z0 + .08 + (z1 - z0 - .16) * k / n
            rk.rib(g, (x0, y, z), (x1, y, z), rise, r, F['frame'], T, power=power)
        for k in range(purlins):
            t = (k + 1) / (purlins + 1)
            x, yy = x0 + (x1 - x0) * t, y + rise * math.sin(math.pi * t) ** power
            rod(g, (x, yy, z0), (x, yy, z1), r * .7, F['frame'], (5, 4, 3)[T.i], caps=False)


def dome_point(a, f):
    """A point of the Well's dome on the meridian at bearing a, f 0 (the wall's top) .. 1 (the housing's collar)."""
    c, s = math.sin(a), math.cos(a)
    k = rk.WELL / max(abs(c), abs(s))                     # the square's edge along this bearing
    ang = f * math.pi / 2
    rr = 1.9 + (k - 1.9) * math.cos(ang) ** .9
    return (c * rr, rk.DOME_SPRING + .12 + (rk.DOME_APEX - .9 - rk.DOME_SPRING) * math.sin(ang), s * rr)


def well_dome(g, T):
    """The Well's dome and the drill: a ring beam on the walls, meridian ribs to the housing's collar, hoops, the drill's
    housing at the crown, the gantry on the four pylons, struts and hydraulic lines up to the housing."""
    w = rk.WELL
    for (a, c, e, h) in ((-w, w, w - .4, w), (-w, w, -w, -w + .4), (-w, -w + .4, -w + .4, w - .4), (w - .4, w, -w + .4, w - .4)):
        box(g, a, c, rk.DOME_SPRING, rk.DOME_SPRING + .24, e, h, F['steel'], T, .02, cell=0)
    m = (24, 16, 8)[T.i]
    seg = (10, 6, 4)[T.i]
    for k in range(m):
        a = 2 * math.pi * (k + .5) / m
        tube(g, [dome_point(a, s / seg) for s in range(seg + 1)], .07 if k % 2 == 0 else .05, F['frame'], (6, 5, 3)[T.i], caps=False)
    for f in ((.3, .58, .82) if T.i < 2 else (.45,)):
        tube(g, [dome_point(2 * math.pi * (k + .5) / m, f) for k in range(m)], .045, F['frame'], (5, 4, 3)[T.i], closed=True)
        if T.detail:
            for k in range(0, m, 2):
                rk.ball(g, dome_point(2 * math.pi * (k + .5) / m, f), .1, F['brass'], 6, 3)
    # the housing: a collared drum hung at the crown, a light ring under it
    sides = (20, 12, 8)[T.i]
    top = rk.DOME_APEX
    lathe(g, [(1.55, top - 1.25), (1.95, top - 1.15), (1.95, top - .8), (1.75, top - .7), (1.75, top - .1), (1.2, top + .25), (.5, top + .4), (.001, top + .4)], (0, 0, 0), F['steel'], sides)
    lathe(g, [(1.95, top - 1.0), (2.02, top - .98), (2.02, top - .9), (1.95, top - .88)], (0, 0, 0), F['brass'], sides)
    lathe(g, [(1.75, top - .45), (1.81, top - .43), (1.81, top - .35), (1.75, top - .33)], (0, 0, 0), F['brass'], sides)
    rk.flat_ring(g, 0, 0, 1.6, 1.9, top - 1.251, F['cyan'], sides, down=True)
    # the gantry: beams between the pylons' heads, struts up to the housing, the hydraulic lines beside them
    p = 4.2
    y = rk.DOME_SPRING + .26
    for (a, c) in (((-p, y, p), (p, y, p)), ((-p, y, -p), (p, y, -p)), ((-p, y, -p), (-p, y, p)), ((p, y, -p), (p, y, p))):
        ibeam(g, [a, c], .5, .32, F['dark'], up=Y, caps=True)
    for sx in (-1, 1):
        for sz in (-1, 1):
            foot, head = (sx * p, y + .25, sz * p), (sx * 1.25, top - 1.2, sz * 1.25)
            rod(g, foot, head, .11, F['steel'], (8, 6, 4)[T.i])
            box(g, sx * p - .5, sx * p + .5, rk.DOME_SPRING, y + .26, sz * p - .5, sz * p + .5, F['steel'], T, .03, cell=0)
            if T.detail:
                o = (.18 * sz, 0, -.18 * sx)
                rod(g, sk._add(foot, o), sk._add(head, o), .035, F['copper'], 5, caps=False)
                rk.ball(g, foot, .2, F['brass'], 8, 4)


def overhead(g, L, T):
    well_dome(g, T)
    for b in L['blocks']:
        i = b['id']
        x0, x1, y0, y1, z0, z1 = ext(b)
        cx, cz = b['pos'][0], b['pos'][2]
        if i.startswith('rig-tunnel-') and ('-floor-' in i or i.endswith('-base')):
            along_z = i.endswith('-floor-0')
            barrel(g, x0 + .2, x1 - .2, z0 + (0 if along_z else .2), z1 - (0 if along_z else .2), 3.2, 1.25, not along_z, T)
            rk.pendant(g, (cx, 4.42, cz), 1.44, T, r=.22)
        elif i.startswith('rig-spine-') and i.endswith('-floor'):
            barrel(g, x0, x1, z0 + .2, z1 - .2, 6.4, 1.15, True, T, r=.06)
            rk.pendant(g, (cx, 7.5, 0), 1.46, T)
            rk.flush_lamp(g, cx, 2.8, 0, .42, .42, T)
        elif i.startswith('rig-wing-') and i.endswith('-floor'):
            barrel(g, x0 + .2, x1 - .2, z0 + .2, z1 - .2, 6.4, 1.9, False, T, every=3.0, r=.07, purlins=5, power=.8)
            sg = 1 if cx > 0 else -1
            for z in (-2.5, 2.5):
                t = (cx + sg * 1.6 - (x0 + .2)) / (x1 - x0 - .4)
                rk.pendant(g, (cx + sg * 1.6, 6.4 + 1.9 * math.sin(math.pi * t) ** .8, z), 6.4 + 1.9 * math.sin(math.pi * t) ** .8 - 5.95, T, r=.3)
            for z in (-4.0, 0.0, 4.0):
                rk.flush_lamp(g, cx + sg * 3.6, 2.8, z, .42, .42, T)
        elif i in ('rig-glass-bay-n', 'rig-glass-bay-s'):
            zs = 1 if cz > 0 else -1
            za, zb = sorted((zs * 10.6, zs * 21.2))
            barrel(g, -8.2, 8.2, za, zb, 6.4, 2.2, False, T, every=3.2, r=.075, purlins=5, power=.8)
            for x in (-6.0, 6.0):
                t = (x + 8.2) / 16.4
                rk.pendant(g, (x, 6.4 + 2.2 * math.sin(math.pi * t) ** .8, zs * 16.2), 6.4 + 2.2 * math.sin(math.pi * t) ** .8 - 5.65, T, r=.34)
            # the gantry crane over the pool: a beam wall to wall, a trolley, the hook hanging well over the sub
            zc = zs * 17.9
            ibeam(g, [(-8.0, 5.3, zc), (8.0, 5.3, zc)], .5, .3, F['yellow'], up=Y, caps=True)
            box(g, -.5, .5, 4.86, 5.06, zc - .32, zc + .32, F['dark'], T, .03, cell=0)
            rod(g, (0, 4.86, zc), (0, 2.5, zc), .025, F['steel'], 4, caps=False)
            lathe(g, [(.02, 2.2), (.12, 2.28), (.14, 2.42), (.06, 2.52)], (0, 0, zc), F['brass'], (8, 6, 4)[T.i])
            if T.i < 2:
                for k in range(8):
                    box(g, -8.0 + k * 2.0, -8.0 + k * 2.0 + 1.0, 5.1, 5.5, zc - .156, zc - .15, F['black'], None, 0, cell=0)
        elif i.startswith('rig-glass-tunnel-') or i.startswith('rig-glass-spine-'):
            # a pipe run under the eaves on both faces
            R, h = run_of(b)
            for side in (-1, 1):
                rod(g, R.at(0, y1 - .32, side * (h + .1)), R.at(R.len, y1 - .32, side * (h + .1)), .05, F['copper'] if side > 0 else F['steel'], (6, 5, 3)[T.i], caps=False)
                if T.detail:
                    u = .6
                    while u < R.len:
                        R.box(g, u - .03, u + .03, y1 - .4, y1 - .22, side * h, side * (h + .16), F['dark'], T, .008)
                        u += 2.4


def hazard_fittings(g, L, T):
    """The vents' frames in the bays' floors and their painted arrows toward the pools."""
    for h in L.get('hazards', []):
        if h['kind'] != 'vent':
            continue
        x, y, z = h['a']
        hx, hz = h['size'][0] / 2 - .5, h['size'][2] / 2 - .3
        d = -1 if x > 0 else 1                               # it blows toward its pool
        for (a, c, e, f) in ((x - hx - .1, x + hx + .1, z - hz - .1, z - hz), (x - hx - .1, x + hx + .1, z + hz, z + hz + .1), (x - hx - .1, x - hx, z - hz, z + hz), (x + hx, x + hx + .1, z - hz, z + hz)):
            box(g, a, c, y, y + .03, e, f, F['brass'], T, .008, cell=0, skip=('-y',))
        g.face(((x - hx, y, z - hz), (x + hx, y, z - hz), (x + hx, y, z + hz), (x - hx, y, z + hz)), F['black'], Y)
        if T.i < 2:
            for k in (-1, 1):
                zz = z + k * (hz + .55)
                g.face(((x + d * .5, y, zz), (x - d * .2, y, zz + .3), (x - d * .2, y, zz - .3)), F['yellow'], Y)


# ---------------------------------------------------------------------------------------------------------------------------
# The scene
# ---------------------------------------------------------------------------------------------------------------------------
def static_scene(L, T):
    g = Geo()
    fl = flights(L)
    low = {}
    for name, bs in fl.items():
        bs = sorted(bs, key=lambda b: b['pos'][1] + b['size'][1] / 2)
        dx, dz = bs[0]['pos'][0] - bs[-1]['pos'][0], bs[0]['pos'][2] - bs[-1]['pos'][2]
        l = math.hypot(dx, dz) or 1.0
        for b in bs:
            low[b['id']] = (dx / l, dz / l)
    for seed, b in enumerate(L['blocks']):
        i = b['id']
        if b.get('role') != 'static':
            continue                                            # the supply cases are block props (rig_props.py)
        fam = i.split('-')[1]
        n0 = len(g.p) // 3
        if i.startswith('rig-glass-'):
            rk.glass_wall(g, b, T, mid='-well-' not in i)
        elif i.startswith('rig-stair-'):
            step(g, b, T, low[i])
        elif i.startswith('rig-rail-'):
            rk.rail(g, b, T)
        elif i.startswith('rig-pylon-'):
            pylon(g, b, T)
        elif i.startswith('rig-console-'):
            console(g, b, T, seed)
        elif i.startswith('rig-screen-'):
            rack(g, b, T, seed)
        elif i.startswith('rig-locker-'):
            lockers(g, b, T, seed)
        elif i.startswith('rig-chamber-'):
            chamber(g, b, T)
        elif i.startswith('rig-sub-'):
            sub(g, b, T)
        elif i.startswith('rig-wall-') or i.startswith('rig-bulkhead-'):
            wall(g, b, T, seed)
        elif i.startswith('rig-catwalk-'):
            catwalk(g, b, T)
        else:
            deck(g, b, T)
        st = STATS.setdefault(fam, [0, 0])
        st[0] += 1
        st[1] += len(g.p) // 3 - n0
    n0 = len(g.p) // 3
    overhead(g, L, T)
    hazard_fittings(g, L, T)
    STATS['overhead'] = [1, len(g.p) // 3 - n0]
    return g


VIEWS = [('wide', (-34, 30, -40), (0, 2, 0), 60), ('top', (0, 62, -.01), (0, 0, 0), 55),
         ('well', (-8.4, 1.7, -7.6), (2.5, 3.2, 3), 78), ('well-up', (5.5, 1.2, -6.5), (-1, 9.5, 1), 80), ('gallery', (-8.6, 4.9, -8.6), (3, 4.4, 4), 75),
         ('wing-a', (-19.4, 1.7, -7.6), (-27, 2.0, 4), 75), ('lounge-a', (-19.2, 4.9, -7.8), (-28, 5.0, 3), 75), ('spine', (-17.0, 1.7, 0.4), (-8, 1.7, 0), 70),
         ('tunnel', (-22.5, 1.7, 10.2), (-22.3, 1.9, 17), 75), ('tunnel-run', (-21.5, 1.7, 15.6), (-9, 1.2, 15.6), 70), ('bay', (-7.2, 0.2, 11.4), (3, -.8, 18), 80),
         ('bay-window', (0, 1.6, 11.6), (0, 2.6, 21), 85), ('catwalk', (0, 4.9, 10.8), (0, 2.0, 20), 80), ('door', (-13.5, 1.6, 0.9), (-18, 1.5, 0), 60),
         ('console', (5.0, 1.3, 6.6), (5.0, .8, 8.8), 55), ('rack', (-20.0, 1.5, 1.0), (-22.2, 1.3, 0), 60), ('chamber', (-4.2, -.2, 14.0), (-6.2, -.4, 11.8), 60),
         ('sub', (3.2, .4, 13.0), (0, -1.2, 16.2), 60), ('outside', (-44, 4, -26), (-10, 2, 0), 60), ('seabed', (30, -5, 36), (0, -2, 6), 65), ('far', (-20, 14, 40), (-78, -2, 52), 50)]


def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument('--layout', required=True)
    ap.add_argument('--out', help='Assets/LocalAssets to write the bank into')
    ap.add_argument('--preview', help='render views of the baked tier')
    ap.add_argument('--preview-tier', type=int, default=0)
    ap.add_argument('--views', default='')
    ap.add_argument('--tiers', default='0,1,2')
    ap.add_argument('--no-ao', action='store_true')
    ap.add_argument('--no-sea', action='store_true')
    ap.add_argument('--static-only', action='store_true')
    ap.add_argument('--stats', action='store_true')
    args = ap.parse_args(argv)
    L = sk.load_layout(args.layout)
    rk.finishes()
    import rig_props as props
    import rig_sea as sea
    sb = sk.StageBank()
    chunks = sk.Chunks(12.0, (-180.0, -180.0), (-2.6, 3.1))
    t0 = time.time()
    for ti in [int(x) for x in args.tiers.split(',')]:
        T = sk.Tier(ti)
        t = time.time()
        STATS.clear()
        g = static_scene(L, T)
        if not args.no_sea:
            n0 = len(g.p) // 3
            g.extend(sea.near(L, T))
            STATS['sea'] = [1, len(g.p) // 3 - n0]
        if args.stats:
            print('corners by family (%s): ' % T.name + ' '.join('%s:%d' % (k, v[1]) for k, v in sorted(STATS.items(), key=lambda q: -q[1][1])))
        print('  coplanar: %d overlapping pairs, %.2f m2, %d faces stacked, %.0f mm at most' % sk.lift_coplanar(g, eps=sk.NEAR_TIER[ti], lift=sk.LIFT_TIER[ti]))
        print('tier %s: static %d faces, %.1f s' % (T.name, g.faces, time.time() - t))
        ao = None
        if not args.no_ao:
            ao = 1.0 - (1.0 - sk.bake_ao([g], distance=(1.3, 1.3, 1.5)[ti], samples=(32, 16, 12)[ti])[0]) * .85
        bk = sk.Bake(g, ao, floors=FLOORS, seed=11)
        ids = chunks.ids(g)
        for cid in np.unique(ids):
            sb.add('static/%d/%d' % (cid, ti), 'static', sk.to_mesh('static/%d/%d' % (cid, ti), g, bk, ids == cid))
        if args.preview and ti == args.preview_tier:
            views = [v for v in VIEWS if not args.views or v[0] in args.views.split(',')]
            extra = []
            if not args.no_sea:
                for name, fg in sea.far(L, sk.Tier(2)):
                    extra.append((fg, sk.Bake(fg, None, floors=(), seed=5)))
            if not args.views or 'rig' in args.views:
                row = Geo()
                for k, (name, pg, rest) in enumerate(props.items(L, T)):
                    s = props.SHOW.get(name, 1.0)
                    row.extend(pg.transformed([s, 0, 0, -9.0 + (k % 7) * 3.0, 0, s, 0, 300.0 - (k // 7) * 3.4, 0, 0, s, 0]))
                extra.append((row, sk.Bake(row, None, floors=(), seed=7)))
                views = views + [('rig', (0, 298.6, 12.5), (0, 297.6, 0), 62), ('rig-b', (-6, 301.5, 5.5), (-4, 299.4, 0), 60), ('rig-c', (5, 301.5, 5.5), (5, 299.4, 0), 60)]
            sk.preview([(g, bk)] + extra, views, args.preview)
        if not args.static_only:
            props.bank_props(sb, L, T, args.no_ao)
        del g
    if not args.no_sea and not args.static_only:
        sea.bank_far(sb, L, args.no_ao)
    if args.out:
        for b in L['blocks']:
            sb.cover(b['id'])
        sb.write(args.out, BANK)
    else:
        for (group, tier), (n, tris, verts) in sorted(sb.stats.items()):
            print('bank %-7s %-5s %4d props %8d triangles %8d vertices' % (group, tier, n, tris, verts))
    print('done in %.1f s' % (time.time() - t0))


if __name__ == '__main__':
    main()
