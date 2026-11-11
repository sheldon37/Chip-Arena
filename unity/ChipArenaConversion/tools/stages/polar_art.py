"""Polaris Station's stage art (docs/arenas/POLAR.md "Stage art", docs/arenas/STAGE-ART.md): the arctic observatory modelled on
the map's blocks, into its own bank ChipArenaStagePolar.

  dotnet run --project Tests/CoreTests.csproj -c Release -- --stage-layout polar LAYOUT.json
  blender --background --factory-startup --python tools/stages/polar_art.py -- --layout LAYOUT.json --out Assets/LocalAssets
  (--preview DIR [--views a,b] [--preview-tier N] renders the baked stage in Blender; --tiers, --no-ao, --static-only, --stats)

This file: the shelf (its wind-packed snow, the channel's blue walls, the cliff at its south edge, the sea), the pressure ridges,
the field (ice crystal formations, heaved floes, drifts, fuel tanks, snowcats, huts, cargo, weather masts, sleds, the vents'
collars) and the buildings (timber cabins, the red labs, the blue generator sheds: plank walls, lit windows, snowed low roofs
with icicles, stovepipes, porches). polar_ship.py: the icebreaker and the observatory. polar_props.py: the rig's moving parts,
the breakable and sinking blocks, the far backdrop. The map mirrors about x = 0: the west half is modelled and mirrored.

Rules the models keep (Tests/PolarArtChecks.cs): below 2 m over its floor the art lies within .25 m of a block; nothing stands
in a spawn; under the shelf (y < -1) there is only the sea.
"""
import argparse
import math
import os
import sys
import time

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stagekit as sk  # noqa: E402
from stagekit import Geo  # noqa: E402
import polar_kit as pk  # noqa: E402
from polar_kit import F, H, bx  # noqa: E402

BANK = 'ChipArenaStagePolar'
FLOORS = (0.0, 2.4, 3.2)
ROOMS = []      # the buildings' footprints (|x|0, |x|1, z0, z1), set from the layout


def ground_y(x, z):
    """The shelf's top: wind-packed snow, a few centimetres of sastrugi (the same either side of x = 0)."""
    P = np.stack([np.abs(np.asarray(x, dtype=np.float64)) * .35, np.zeros_like(np.asarray(x, dtype=np.float64)), np.asarray(z, dtype=np.float64) * 1.1], axis=-1)
    n = pk.noise(P, .5, 41, 2).reshape(np.shape(x))
    y = .012 + .05 * n
    # under a building's floor the snow lies a little lower (its boards never share the snow's plane)
    ax, zz = np.abs(np.asarray(x, dtype=np.float64)), np.asarray(z, dtype=np.float64)
    for (x0, x1, z0, z1) in ROOMS:
        y = np.where((ax > x0) & (ax < x1) & (zz > z0) & (zz < z1), -.03, y)
    return y


def ground(g, x0, x1, z0, z1, T):
    c = (.8, 2.6, 5.0)[T.i]
    nx = max(2, int(math.ceil((x1 - x0) / c)) + 1)
    nz = max(2, int(math.ceil((z1 - z0) / c)) + 1)
    X, Z = np.meshgrid(np.linspace(x0, x1, nx), np.linspace(z0, z1, nz), indexing='ij')
    Y = ground_y(X, Z) if T.i < 2 else np.full_like(X, .012)
    # flat along the edges that meet walls, water and the cliff, so the pieces join
    P = np.stack([X, Y, Z], axis=2)
    pk.surf(g, P, F['snow'], facing=(0, 1, 0))


def ice_wall(g, a, b, T, facing, y1=0.0, y0=-1.5, seed=0, out=.0):
    """A wall of blue ice from a to b (x, z), from the shelf's lip down under the water: layered, faceted."""
    n = max(2, int(math.ceil(math.hypot(b[0] - a[0], b[1] - a[1]) / (.7, 1.6, 3.2)[T.i])) + 1)
    rows = [(y1 + .014, 0.0), (y1 - .12, .03), (y1 - .5, .0), (-.95, .05), (-1.3, .0), (y0 - .8, -.1)]
    P = np.zeros((n, len(rows), 3))
    fx, fz = facing
    for i in range(n):
        u = i / (n - 1)
        x, z = a[0] + (b[0] - a[0]) * u, a[1] + (b[1] - a[1]) * u
        for j, (y, d) in enumerate(rows):
            # (the wall leans back from the lip: never proud of the block over the water)
            w = (d + (.06 * H(seed, i, j) if 0 < j < len(rows) - 1 else 0) + out * (j / (len(rows) - 1)) ** 2) * (0 if (i == 0 or i == n - 1) and out == 0 else 1)
            P[i, j] = (x - fx * w * (1 if out == 0 else -1), y, z - fz * w * (1 if out == 0 else -1))
    pk.surf(g, P, F['ice-deep'], facing=(fx, .2, fz), flat=True, pick=lambda nn, cc: F['snow'] if cc[1] > y1 - .07 else (F['ice'] if cc[1] > -.4 else (F['ice-deep'] if cc[1] > -1.2 else F['ice-dark'])))


def shelf_west(g, L, T):
    P = L['polar']
    ch, zn, zs, edge = P['channelHalf'], P['channelN'], P['channelS'], P['shelfZ']
    ground(g, -P['shelfX'], -ch, edge, P['shelfN'], T)
    # the channel's west bank (the ponds' walls) and the shelf's cliff into the sea
    ice_wall(g, (-ch, zs), (-ch, -1.2), T, (1, 0), seed=1)
    ice_wall(g, (-ch, 1.2), (-ch, zn), T, (1, 0), seed=2)
    ice_wall(g, (-P['shelfX'], edge), (0, edge), T, (0, -1), y0=-3.0, seed=3, out=1.4)


def shelf_centre(g, L, T):
    P = L['polar']
    ch, zn, zs, edge, sea = P['channelHalf'], P['channelN'], P['channelS'], P['shelfZ'], P['sea']
    for (z0, z1) in ((edge, zs), (-1.2, 1.2), (zn, 8.3)):
        ground(g, -ch, ch, z0, z1, T)
    for (z, f) in ((zs, 1), (-1.2, -1), (1.2, 1), (zn, -1)):
        ice_wall(g, (-ch, z), (ch, z), T, (0, f), seed=int(z * 10) % 97)
    # the water in the channel and, past the edge, out to the play space's bound (the far sea is the backdrop's)
    sk.grid_face(g, (-ch, sea, zs), (2 * ch, 0, 0), (0, 0, zn - zs), 0, 0, F['water'], (0, 1, 0), (1.5, 4, 8)[T.i])
    sk.grid_face(g, (-60, sea, edge - 36), (120, 0, 0), (0, 0, 36 + 1.2), 0, 0, F['water'], (0, 1, 0), (4, 8, 16)[T.i])
    # brash ice against the cliff's foot
    if T.detail:
        for k in range(22):
            x = (H(k, 71) - .5) * 64
            z = edge - 1.2 - H(k, 72) * 5.5
            r = .35 + H(k, 73) * .8
            o = pk.ngon(r, 6, phase=H(k, 74) * 3, sx=1, sz=.6 + .4 * H(k, 75))
            pk.loft(g, [[(x + a * .9, sea - .1, z + b * .9) for a, b in o], [(x + a, sea + .08, z + b) for a, b in o], [(x + a * .8, sea + .14 + k * .004, z + b * .8) for a, b in o]], F['ice'], cap0=False, cap1=True, flat=True, f_cap=F['snow'])


# ---------------------------------------------------------------------------------------------------------------------------
# Pressure ridges
# ---------------------------------------------------------------------------------------------------------------------------
def ridge(g, T, along, heights, place, seed, ends=(0.0, 0.0)):
    """A pressure ridge: tumbled ice along a line. along: (s0, s1); heights(s) -> (foot top, back top); place(s, c) -> (x, z)
    with c the distance in from its inner face (0) to its back (2) and down its outer slope (to 5). Faceted; snow on what
    faces up. Below 2 m its inner face keeps to the block."""
    s0, s1 = along
    n = max(3, int(math.ceil((s1 - s0) / (.85, 1.9, 4.0)[T.i])) + 1)
    prof = [(0.0, 0.0, 0), (.03, .7, 0), (.08, 1.4, 0), (.14, 2.05, 0), (.3, .62, 1), (.6, .9, 1), (.95, 1.04, 1), (1.3, .5, 2), (1.6, 1.0, 2), (2.1, .96, 2), (3.1, .55, 2), (4.4, .22, 2), (5.6, .05, 2)]
    P = np.zeros((n, len(prof), 3))
    for i in range(n):
        s = s0 + (s1 - s0) * i / (n - 1)
        yf, yb = heights(s)
        # (toward an end that falls away, the ridge sinks)
        e = 1.0
        if ends[0] > 0 and s < s0 + ends[0]:
            e = max(.12, (s - s0) / ends[0])
        if ends[1] > 0 and s > s1 - ends[1]:
            e = max(.12, (s1 - s) / ends[1])
        for j, (c, v, kind) in enumerate(prof):
            y = v if kind == 0 else (v * yf if kind == 1 else (yf + (yb - yf) * v if j < 9 else yb * v))
            if kind == 0:
                y = min(y, yf * .5)
            rough = (.0 if y < 2.0 else .55) * (H(seed, i, j) - .5) * 2
            jit = (.0 if kind == 0 else .28) * (H(seed, i, j, 5) - .5)
            x, z = place(s + (0 if kind == 0 else jit), c + (0 if kind == 0 else abs(rough) * .25))
            P[i, j] = (x, (y + rough * (.0 if kind == 0 else 1)) * e, z)
    def pick(nn, cc):
        if nn[1] > .5:
            return F['snow']
        # blue where the blocks are sheer, deeper toward their feet and in the clefts between them
        return F['ice-deep'] if (cc[1] < 1.1 or H(seed, int(cc[0] * .45), int(cc[2] * .45)) > .72) else F['ice']
    pk.surf(g, P, F['ice'], facing=(0, 1, 0), flat=True, pick=pick)


def ridges_west(g, L, T):
    B = {b['id']: b for b in L['blocks']}
    def table(prefix):
        rows = []
        k = 0
        while '%s-%d-foot' % (prefix, k) in B:
            f, b = bx(B['%s-%d-foot' % (prefix, k)]), bx(B['%s-%d-back' % (prefix, k)])
            rows.append((f, b))
            k += 1
        return rows
    west = table('polar-ridge-w')
    zs = [(f[4] + f[5]) / 2 for f, b in west]
    def hw(s):
        return float(np.interp(s, zs, [f[3] for f, b in west])), float(np.interp(s, zs, [b[3] for f, b in west]))
    xin = west[0][0][1]
    ridge(g, T, (west[0][0][4] - 3.5, west[-1][0][5] + 4), hw, lambda s, c: (xin - c, s), 7, ends=(3.5, 0))
    north = table('polar-ridge-n-w')
    xs = [(f[0] + f[1]) / 2 for f, b in north]
    def hn(s):
        return float(np.interp(s, xs, [f[3] for f, b in north])), float(np.interp(s, xs, [b[3] for f, b in north]))
    zin = north[0][0][4]
    ridge(g, T, (north[0][0][0] - 1.0, north[-1][0][1] - .05), hn, lambda s, c: (s, zin + c), 9)


# ---------------------------------------------------------------------------------------------------------------------------
# The field
# ---------------------------------------------------------------------------------------------------------------------------
def spire(g, b, T, seed):
    """An ice crystal formation filling its block: a ring of leaning six-sided crystals round a tall one, lit seams down them,
    snow at their feet. The feet stand within the block; the tips lean out only above 2 m."""
    x0, x1, y0, y1, z0, z1 = bx(b)
    cx, cz, hx, hz, h = (x0 + x1) / 2, (z0 + z1) / 2, (x1 - x0) / 2, (z1 - z0) / 2, y1 - y0
    # a core that fills the block to within a hand (what looks like cover is cover), then the crystals over it
    o = pk.ngon(1.0, 6, phase=H(seed, 1) * 1.0)
    rings = [[(cx + a * hx * 1.04, y0, cz + c * hz * 1.04) for a, c in o], [(cx + a * hx * 1.0, y0 + min(2.0, h * .6), cz + c * hz * 1.0) for a, c in o],
             [(cx + a * hx * .55, y0 + h * .86, cz + c * hz * .55) for a, c in o]]
    pk.loft(g, rings, F['ice-deep'], cap0=False, cap1=True, flat=True, f_cap=F['ice'])
    n = (7, 5, 3)[T.i]
    for k in range(n):
        a = k * 2 * math.pi / n + H(seed, k) * .6
        hh = h * (.62 + .34 * H(seed, k, 3))
        lean = .1 + .1 * H(seed, k, 4)
        r = min(.3 + .14 * H(seed, k, 5), min(hx, hz) * .42)
        # (below 2 m a crystal keeps within a hand of its block: its foot stands in by its radius and by what it leans there)
        m = r + lean * min(2.0, hh) - .16
        bxp = cx + max(-(hx - m), min(hx - m, math.cos(a) * hx * 1.5))
        bzp = cz + max(-(hz - m), min(hz - m, math.sin(a) * hz * 1.5))
        tip = (bxp + math.cos(a) * hh * lean, y0 + hh, bzp + math.sin(a) * hh * lean)
        pk.crystal(g, (bxp, y0 - .05, bzp), tip, r, F['ice'] if k % 2 else F['ice-deep'], phase=H(seed, k, 6), f_core=F['ice-glow'] if k % 2 == 0 else None, T=T)
    pk.crystal(g, (cx, y0 + h * .3, cz), (cx + (H(seed, 9) - .5) * .5, y1 + .25, cz + (H(seed, 10) - .5) * .5), min(hx, hz) * .5, F['ice'], phase=.3, f_core=F['ice-glow'], T=T)
    pk.mound(g, x0 - .18, x1 + .18, z0 - .18, z1 + .18, y0, .2, F['snow'], T, seed=seed, steep=.3, bulge=.04, cell=.5)


def drift(g, apron, crest, T, seed):
    x0, x1, y0, y1, z0, z1 = bx(crest)
    a = bx(apron)
    # one wind-packed heap: its skirt out over the apron, its crest over the crest block (a cornice leaning with the wind)
    c = (.4, .8, 1.6)[T.i]
    nx = max(4, int((a[1] - a[0]) / c) + 1)
    nz = max(4, int((a[5] - a[4]) / c) + 1)
    X, Z = np.meshgrid(np.linspace(a[0] - .1, a[1] + .1, nx), np.linspace(a[4] - .1, a[5] + .1, nz), indexing='ij')
    def plateau(v, lo, hi, w):
        return np.clip(np.minimum(v - lo, hi - v) / w, 0, 1)
    skirt = np.minimum(plateau(X, a[0] - .1, a[1] + .1, .45), plateau(Z, a[4] - .1, a[5] + .1, .45)) ** .7
    top = np.minimum(plateau(X, x0, x1, .26), plateau(Z, z0, z1, .26)) ** .6
    n = pk.noise(np.stack([X, np.zeros_like(X), Z], axis=-1), 1.1, seed + 3, 2).reshape(X.shape)
    Y = skirt * a[3] + top * (y1 - a[3] + .06 + .1 * n * top)
    pk.surf(g, np.stack([X, Y, Z], axis=2), F['snow'], facing=(0, 1, 0), pick=lambda nn, cc: F['snow'] if nn[1] > .6 else F['snow-shade'])


def tank(g, b, T, seed):
    """A fuel tank: an upright drum on a skid with a post at each corner (the block's corners are filled), hoops, a ladder,
    a hazard band, snow on its head."""
    x0, x1, y0, y1, z0, z1 = bx(b)
    cx, cz, r = (x0 + x1) / 2, (z0 + z1) / 2, min(x1 - x0, z1 - z0) / 2
    sk.box(g, x0, x1, y0, y0 + .28, z0, z1, F['steel-dark'], T, bevel=.04, cell=0, skip=('-y',))
    for (px, pz) in ((x0 + .1, z0 + .1), (x1 - .1, z0 + .1), (x0 + .1, z1 - .1), (x1 - .1, z1 - .1)):
        sk.box(g, px - .1, px + .1, y0 + .28, y1 - .5, pz - .1, pz + .1, F['steel-dark'], T, bevel=.02, cell=0)
    for (ax0, ax1, bz0, bz1) in ((x0, x1, z0, z0 + .14), (x0, x1, z1 - .14, z1), (x0, x0 + .14, z0 + .14, z1 - .14), (x1 - .14, x1, z0 + .14, z1 - .14)):
        sk.box(g, ax0, ax1, y1 - .64, y1 - .5, bz0, bz1, F['steel-dark'], T, bevel=.02, cell=0)
    sides = (20, 12, 8)[T.i]
    prof = [(r * .9, .28), (r - .02, .36), (r - .02, 1.3), (r - .02, 1.3), (r - .02, 1.62), (r - .02, y1 - y0 - .34), (r * .86, y1 - y0 - .12), (r * .5, y1 - y0 - .02), (.01, y1 - y0)]
    sk.lathe(g, prof[:3], (cx, y0, cz), F['white'], sides)
    sk.lathe(g, prof[3:5], (cx, y0, cz), F['yellow'], sides)
    sk.lathe(g, prof[4:], (cx, y0, cz), F['white'], sides)
    for y in (.7, 2.2):
        sk.lathe(g, [(r - .02, y), (r + .02, y + .03), (r + .02, y + .09), (r - .02, y + .12)], (cx, y0, cz), F['steel-dark'], sides)
    if T.detail:
        s = -1 if cx < 0 else 1
        for dz in (-.2, .2):
            pk.pipe(g, (cx - s * (r + .04), y0 + .3, cz + dz), (cx - s * (r + .04), y1 - .3, cz + dz), .025, F['steel'], T)
        for y in np.arange(y0 + .5, y1 - .3, .34):
            pk.pipe(g, (cx - s * (r + .04), y, cz - .2), (cx - s * (r + .04), y, cz + .2), .02, F['steel'], T, caps=False)
    pk.mound(g, cx - r * .62, cx + r * .62, cz - r * .62, cz + r * .62, y1 - .1, .16, F['snow'], T, seed=seed, steep=.3, bulge=.05, cell=.4)


def snowcat(g, b, blade, T):
    """A snowcat: tracks with their wheels, an orange body, a cab with lit windows, lamps and a rack; its blade before it."""
    x0, x1, y0, y1, z0, z1 = bx(b)
    cx = (x0 + x1) / 2
    # tracks (their belts round the wheels), the full length either side
    for s in (-1, 1):
        tx0, tx1 = (x0, x0 + .62) if s < 0 else (x1 - .62, x1)
        n = (10, 6, 4)[T.i]
        path = []
        for k in range(n + 1):
            a = math.pi / 2 + math.pi * k / n
            path.append((z0 + .42 + math.cos(a) * .42, y0 + .42 + math.sin(a) * .42))
        for k in range(n + 1):
            a = -math.pi / 2 + math.pi * k / n
            path.append((z1 - .42 + math.cos(a) * .42, y0 + .42 + math.sin(a) * .42))
        rings = [[(tx0, y, z) for z, y in path], [(tx1, y, z) for z, y in path]]
        pk.loft(g, rings, F['rubber'], cap0=True, cap1=True)
        if T.detail:
            for z in np.arange(z0 + .5, z1 - .3, .82):
                sk.lathe(g, [(.3, 0), (.3, .04)], ((tx0 if s < 0 else tx1) + (-.04 if s < 0 else 0), y0 + .42, z), F['steel-dark'], 8, axis=(1, 0, 0), cap0=True, cap1=True)
    # the body between and over the tracks, the cab forward (toward +z, where its blade is)
    pk.body(g, x0 + .1, x1 - .1, y0 + .5, y0 + 1.35, z0 + .05, z1 - .05, F['orange'], r=.16, T=T)
    pk.body(g, x0 + .14, x1 - .14, y0 + 1.3, y1, z1 - 2.5, z1 - .2, F['orange'], r=.2, T=T, f_top=F['white'], taper=.08)
    sk.box(g, x0 + .2, x1 - .2, y0 + 1.3, y0 + 1.75, z0 + .15, z1 - 2.5, F['steel-dark'], T, bevel=.05, cell=0)
    for s, face in ((-1, '-x'), (1, '+x')):
        pk.frame_window(g, face, z1 - 2.3, z1 - .45, y0 + 1.5, y0 + 2.2, (x0 + .14) if s < 0 else (x1 - .14), T, F['black'], F['window-dim'], bars=(1, 0), sill=False)
    pk.frame_window(g, '+z', x0 + .4, x1 - .4, y0 + 1.5, y0 + 2.2, z1 - .2, T, F['black'], F['window-dim'], bars=(1, 0), sill=False)
    for x in (x0 + .45, x1 - .45):
        sk.box(g, x - .13, x + .13, y0 + .95, y0 + 1.2, z1 - .06, z1 + .0, F['lamp'], T, bevel=0, cell=0)
    if T.detail:
        sk.box(g, x0 + .3, x1 - .3, y1, y1 + .06, z1 - 2.3, z1 - .5, F['snow'], T, bevel=.02, cell=0)
        for x in (x0 + .5, x1 - .5):
            sk.box(g, x - .1, x + .1, y1, y1 + .16, z1 - .5, z1 - .3, F['lamp'], T, bevel=.02, cell=0)
    # the blade: a curved mouldboard on two arms
    a0, a1, c0, c1, d0, d1 = bx(blade)
    n = (6, 4, 2)[T.i]
    P = np.zeros((2, n + 1, 3))
    for j in range(n + 1):
        v = j / n
        P[0, j] = (a0, c0 + v * (c1 - c0), d1 - .06 - .16 * math.sin(v * math.pi))
        P[1, j] = (a1, c0 + v * (c1 - c0), d1 - .06 - .16 * math.sin(v * math.pi))
    pk.surf(g, P, F['yellow'], facing=(0, 0, 1))
    sk.box(g, a0, a1, c0, c1, d0, d1 - .3, F['steel-dark'], T, bevel=.03, cell=0)
    for x in (a0 + .5, a1 - .5):
        sk.box(g, x - .08, x + .08, c0 + .3, c0 + .5, d0 - .5, d0 + .1, F['steel-dark'], T, bevel=.02, cell=0)


def hut(g, b, T, seed):
    """An instrument hut: a louvred timber box on a plinth with a hipped, snowed roof and a lit window."""
    x0, x1, y0, y1, z0, z1 = bx(b)
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    sk.box(g, x0, x1, y0, y0 + .3, z0, z1, F['concrete-dark'], T, bevel=.03, cell=0, skip=('-y',))
    planks(g, x0 + .04, x1 - .04, y0 + .3, y1 - .5, z0 + .04, z1 - .04, T, F['timber'], F['timber-z'], .22)
    for (px, pz) in ((x0 + .06, z0 + .06), (x1 - .06, z0 + .06), (x0 + .06, z1 - .06), (x1 - .06, z1 - .06)):
        sk.box(g, px - .06, px + .06, y0 + .3, y1 - .5, pz - .06, pz + .06, F['timber-dark'], T, bevel=.015, cell=0)
    roof(g, x0 - .12, x1 + .12, z0 - .12, z1 + .12, y1 - .42, .42, T, seed, hip=True)
    s = 1 if cx < 0 else -1
    pk.frame_window(g, '+x' if s > 0 else '-x', cz - .45, cz + .45, y0 + 1.25, y0 + 1.85, (x1 - .04) if s > 0 else (x0 + .04), T, F['timber-dark'], F['window'], bars=(1, 1))
    sk.box(g, cx - .42, cx + .42, y0 + .3, y0 + 2.0, z1 - .05, z1 + .0, F['timber-dark'], T, bevel=.02, cell=0)


def stack(g, b, T, seed):
    """Tarped cargo on pallets: a lumpy tarp roped down."""
    x0, x1, y0, y1, z0, z1 = bx(b)
    sk.box(g, x0 + .03, x1 - .03, y0, y0 + .16, z0 + .03, z1 - .03, F['timber-dark'], T, bevel=.02, cell=0, skip=('-y',))
    n = (4, 3, 2)[T.i]
    o = pk.rrect((x1 - x0) / 2, (z1 - z0) / 2, .3, n)
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    rings = []
    for k, (y, s) in enumerate(((y0 + .16, .99), (y0 + .9, 1.0), (y0 + 1.7, .98), (y1 - .3, .94), (y1 - .04, .8), (y1 + .02, .5))):
        rings.append([(cx + x * s + (H(seed, k, m) - .5) * .09, y + (H(seed, k, m, 2) - .5) * .06, cz + z * s + (H(seed, m, k) - .5) * .09) for m, (x, z) in enumerate(o)])
    pk.loft(g, rings, F['tarp'], cap0=False, cap1=True)
    if T.detail:
        for u in (-.5, .5):
            path = [(cx + u, y0 + .2, z0 - .03), (cx + u, y1 - .2, z0 - .03), (cx + u, y1 + .06, cz), (cx + u, y1 - .2, z1 + .03), (cx + u, y0 + .2, z1 + .03)]
            sk.tube(g, path, .022, F['rope'], 5, caps=False)
    pk.mound(g, x0 + .3, x1 - .3, z0 + .3, z1 - .3, y1 - .02, .1, F['snow'], T, seed=seed, steep=.3, bulge=.04, cell=.45)


def mast(g, b, T, seed):
    """A weather mast: a concrete foot, a lattice of four legs braced in bays, instrument arms, a lamp (the anemometer on its
    head is the rig's)."""
    x0, x1, y0, y1, z0, z1 = bx(b)
    cx, cz, h = (x0 + x1) / 2, (z0 + z1) / 2, (x1 - x0) / 2
    pk.body(g, x0, x1, y0, y0 + .6, z0, z1, F['concrete'], r=.08, T=T)
    legs = [(-1, -1), (1, -1), (1, 1), (-1, 1)]
    def at(k, y):
        t = (y - y0) / (y1 - y0)
        w = h * (.9 - .55 * t)
        return (cx + legs[k][0] * w, y, cz + legs[k][1] * w)
    for k in range(4):
        pk.pipe(g, at(k, y0 + .6), at(k, y1 - .1), .045, F['white'] if T.i else F['white'], T)
    bays = (8, 4, 0)[T.i]
    for j in range(bays):
        ya, yb = y0 + .6 + (y1 - y0 - .7) * j / bays, y0 + .6 + (y1 - y0 - .7) * (j + 1) / bays
        for k in range(4):
            k1 = (k + 1) % 4
            pk.pipe(g, at(k, ya), at(k1, yb) if j % 2 == 0 else at(k1, ya), .02, F['red'] if j % 4 < 2 else F['white'], T, caps=False)
            pk.pipe(g, at(k, yb), at(k1, yb), .02, F['red'] if j % 4 < 2 else F['white'], T, caps=False)
    if T.i == 2:
        sk.box(g, cx - h * .5, cx + h * .5, y0 + .6, y1 - .1, cz - h * .5, cz + h * .5, F['white'], T, bevel=0, cell=0)
    sk.box(g, cx - .22, cx + .22, y1 - .14, y1, cz - .22, cz + .22, F['steel-dark'], T, bevel=.02, cell=0)
    sk.box(g, cx - .07, cx + .07, y1 - .5, y1 - .3, cz + .2, cz + .34, F['beacon'], T, bevel=0, cell=0)
    if T.detail:
        sk.box(g, cx - 1.1, cx + 1.1, y0 + 6.0, y0 + 6.06, cz - .03, cz + .03, F['steel'], T, bevel=0, cell=0)
        for x in (cx - 1.05, cx + 1.05):
            sk.box(g, x - .1, x + .1, y0 + 6.06, y0 + 6.4, cz - .1, cz + .1, F['white'], T, bevel=.03, cell=0)
        sk.box(g, cx - .32, cx + .32, y0 + 2.4, y0 + 3.0, cz - h * .7 - .05, cz - h * .7, F['glass'], T, bevel=.01, cell=0)


def sled(g, b, T, seed):
    """A cargo sled: runners, a deck, crates and drums lashed under a net."""
    x0, x1, y0, y1, z0, z1 = bx(b)
    for z in (z0 + .1, z1 - .1):
        sk.box(g, x0, x1, y0, y0 + .12, z - .08, z + .08, F['timber-dark'], T, bevel=.03, cell=0, skip=('-y',))
    sk.box(g, x0 + .05, x1 - .05, y0 + .12, y0 + .24, z0 + .02, z1 - .02, F['timber'], T, bevel=.02, cell=0)
    xs = np.linspace(x0 + .1, x1 - .1, 4)
    for k in range(3):
        if k == 1:
            for j in range(2):
                sk.lathe(g, [(.27, 0), (.29, .05), (.29, y1 - y0 - .3), (.27, y1 - y0 - .26)], ((xs[k] + xs[k + 1]) / 2, y0 + .24, z0 + .3 + j * (z1 - z0 - .6)), F['drum'], (10, 6, 6)[T.i], cap1=True)
        else:
            pk.body(g, xs[k] + .03, xs[k + 1] - .03, y0 + .24, y1 - .02 * k, z0 + .06, z1 - .06, F['timber-z'], r=.03, T=T)
    if T.detail:
        for x in (x0 + .5, x1 - .5):
            sk.tube(g, [(x, y0 + .2, z0 - .02), (x, y1 + .01, z0 + .05), (x, y1 + .01, z1 - .05), (x, y0 + .2, z1 + .02)], .02, F['rope'], 5, caps=False)


def vent(g, h, T):
    """A steam vent's collar on the shelf: a steel ring with a grate and a hazard-striped rim (its lamps are the rig's)."""
    x, y, z = h['a']
    r = h['size'][0] / 2
    sides = (20, 12, 8)[T.i]
    sk.lathe(g, [(r, 0), (r, .05), (r - .14, .07), (r - .16, .03)], (x, y + .012, z), F['steel-dark'], sides)
    for k in range(sides):
        a0, a1 = 2 * math.pi * k / sides, 2 * math.pi * (k + 1) / sides
        g.face(((x + math.cos(a0) * r, y + .064, z + math.sin(a0) * r), (x + math.cos(a1) * r, y + .064, z + math.sin(a1) * r),
                (x + math.cos(a1) * (r - .13), y + .084, z + math.sin(a1) * (r - .13)), (x + math.cos(a0) * (r - .13), y + .084, z + math.sin(a0) * (r - .13))),
               F['yellow'] if k % 2 == 0 else F['black'], normal=(0, 1, 0))
    g.face([(x + c * (r - .16), y + .03, z + s * (r - .16)) for c, s in pk.ngon(1.0, sides)], F['black'], normal=(0, 1, 0))
    if T.detail:
        for u in np.arange(-r + .28, r - .2, .2):
            w = math.sqrt(max(0.0, (r - .16) ** 2 - u * u))
            sk.box(g, x + u - .03, x + u + .03, y + .03, y + .07, z - w, z + w, F['steel'], T, bevel=0, cell=0)


# ---------------------------------------------------------------------------------------------------------------------------
# Buildings
# ---------------------------------------------------------------------------------------------------------------------------
def planks(g, x0, x1, y0, y1, z0, z1, T, fx, fz, course=.2):
    """A timber wall: courses of planks on the Fine tier (each a little proud of the next), one board below it."""
    f = fx if (x1 - x0) >= (z1 - z0) else fz
    if T.i > 0:
        sk.box(g, x0, x1, y0, y1, z0, z1, f, T, bevel=.02, cell=3.0, skip=('-y',))
        return
    n = max(1, int(round((y1 - y0) / course)))
    for k in range(n):
        a, b = y0 + (y1 - y0) * k / n, y0 + (y1 - y0) * (k + 1) / n
        d = .012 * (k % 2)
        sk.box(g, x0 + d, x1 - d, a, b, z0 + d, z1 - d, f, T, bevel=.018, cell=2.5, skip=('-y',) if k else ('-y',))


def roof(g, x0, x1, z0, z1, y, rise, T, seed, hip=False, pipe_at=None):
    """A low roof over a rectangle: steel sheets with standing seams, a thick lid of snow rounded at its eaves, icicles."""
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    along_z = (z1 - z0) >= (x1 - x0)
    # the sheet: two slopes from the ridge (along the long side), or four to a point
    def hgt(x, z):
        if hip:
            return rise * (1 - max(abs(x - cx) / ((x1 - x0) / 2), abs(z - cz) / ((z1 - z0) / 2)))
        return rise * (1 - (abs(x - cx) / ((x1 - x0) / 2) if along_z else abs(z - cz) / ((z1 - z0) / 2)))
    c = (.6, 1.4, 3.0)[T.i]
    nx = max(3, int((x1 - x0) / c) | 1) + 1
    nz = max(3, int((z1 - z0) / c) | 1) + 1
    X, Z = np.meshgrid(np.linspace(x0, x1, nx), np.linspace(z0, z1, nz), indexing='ij')
    H0 = np.vectorize(hgt)(X, Z)
    # the eaves' fascia and the soffit
    sk.box(g, x0, x1, y - .14, y + .02, z0, z1, F['roof'], T, bevel=.02, cell=0)
    # snow: thick, swelling, rolled over at the eaves
    def edge(v, lo, hi):
        return np.clip(np.minimum(v - lo, hi - v) / .3, 0, 1) ** .5
    e = np.minimum(edge(X, x0 - .06, x1 + .06), edge(Z, z0 - .06, z1 + .06))
    n = pk.noise(np.stack([X, np.zeros_like(X), Z], axis=-1), .8, seed + 21, 2).reshape(X.shape)
    Y = y + .02 + H0 + e * (.15 + .06 * n)
    X2 = cx + (X - cx) * (1 + .012)
    Z2 = cz + (Z - cz) * (1 + .012)
    pk.surf(g, np.stack([X2, Y, Z2], axis=2), F['snow'], facing=(0, 1, 0), pick=lambda nn, cc: F['snow'] if nn[1] > .7 else F['snow-shade'])
    # icicles under the eaves (over 2 m up)
    if T.detail:
        k = 0
        for (a0, a1, fixed, ax) in ((x0, x1, z0, 'x'), (x0, x1, z1, 'x'), (z0, z1, x0, 'z'), (z0, z1, x1, 'z')):
            u = a0 + .2
            while u < a1 - .2:
                k += 1
                ln = .14 + .34 * H(seed, k, 31)
                if H(seed, k, 32) > .35:
                    p = (u, y - .14, fixed) if ax == 'x' else (fixed, y - .14, u)
                    sk.lathe(g, [(.001, -ln), (.03, -ln * .35), (.045, 0)], p, F['ice'], (5, 4, 4)[T.i])
                u += .22 + .5 * H(seed, k, 33)
    if pipe_at is not None:
        px, pz = pipe_at
        top = y + hgt(px, pz) + .12
        sk.lathe(g, [(.11, 0), (.11, .36), (.2, .4), (.2, .44), (.02, .56)], (px, top, pz), F['steel-dark'], (8, 6, 5)[T.i])


def building(g, L, T, prefix, kind):
    """A cabin, a lab or a shed from its wall blocks: plank or sheet walls with a base course and corner posts, lit windows in
    its outer faces, door frames with a lamp over each, its low snowed roof with a stovepipe."""
    B = [b for b in L['blocks'] if b['id'].startswith(prefix)]
    walls = [b for b in B if '-wall' in b['id'] or '-lintel' in b['id']]
    rb = [b for b in B if b['id'].endswith('-roof')][0]
    mid = ((bx(rb)[0] + bx(rb)[1]) / 2, (bx(rb)[4] + bx(rb)[5]) / 2)
    fa, fb = (F['timber'], F['timber-z']) if kind == 'cabin' else ((F['red-paint'], F['red-paint']) if kind == 'lab' else (F['blue-paint'], F['blue-paint']))
    frame = F['timber-dark'] if kind != 'shed' else F['steel-dark']
    seed = int(abs(mid[1]) * 3) + 1
    for b in walls:
        x0, x1, y0, y1, z0, z1 = bx(b)
        along_z = (x1 - x0) < (z1 - z0)
        lintel = '-lintel' in b['id']
        if kind == 'cabin':
            planks(g, x0, x1, y0, y1, z0, z1, T, fa, fb)
        else:
            sk.box(g, x0, x1, y0, y1, z0, z1, fa, T, bevel=.02, skip=('-y',))
            if T.i == 0 and not lintel:
                # sheet seams: upright ribs on both faces
                for u in np.arange((z0 if along_z else x0) + .3, (z1 if along_z else x1) - .1, .6):
                    if along_z:
                        sk.box(g, x0 - .012, x1 + .012, y0 + .3, y1, u - .025, u + .025, fa, T, bevel=.01, cell=0)
                    else:
                        sk.box(g, u - .025, u + .025, y0 + .3, y1, z0 - .012, z1 + .012, fa, T, bevel=.01, cell=0)
        if lintel:
            # the door's frame: a jamb each side, down to the floor, and a head trim
            if along_z:
                for zz in (z0, z1):
                    sk.box(g, x0 - .04, x1 + .04, 0, y1, zz - .07, zz + .07, frame, T, bevel=.02, cell=0, skip=('-y',))
                sk.box(g, x0 - .04, x1 + .04, y0 - .02, y0 + .1, z0, z1, frame, T, bevel=.02, cell=0)
            else:
                for xx in (x0, x1):
                    sk.box(g, xx - .07, xx + .07, 0, y1, z0 - .04, z1 + .04, frame, T, bevel=.02, cell=0, skip=('-y',))
                sk.box(g, x0, x1, y0 - .02, y0 + .1, z0 - .04, z1 + .04, frame, T, bevel=.02, cell=0)
            # the lamp over the door, outside
            out = (1 if (x0 + x1) / 2 > mid[0] else -1) if along_z else (1 if (z0 + z1) / 2 > mid[1] else -1)
            if along_z:
                px = x1 if out > 0 else x0
                sk.box(g, px + (0 if out > 0 else -.12), px + (.12 if out > 0 else 0), y0 + .2, y0 + .36, (z0 + z1) / 2 - .22, (z0 + z1) / 2 + .22, F['lamp'], T, bevel=.02, cell=0)
            else:
                pz = z1 if out > 0 else z0
                sk.box(g, (x0 + x1) / 2 - .22, (x0 + x1) / 2 + .22, y0 + .2, y0 + .36, pz + (0 if out > 0 else -.12), pz + (.12 if out > 0 else 0), F['lamp'], T, bevel=.02, cell=0)
            continue
        # the base course
        sk.box(g, x0 - .03, x1 + .03, y0, y0 + .32, z0 - .03, z1 + .03, F['concrete-dark'] if kind != 'shed' else F['steel-dark'], T, bevel=.02, cell=0, skip=('-y',))
        # windows in the outer face (none in a cabin's back wall, a metre from the ridge)
        ln = (z1 - z0) if along_z else (x1 - x0)
        if kind == 'cabin' and along_z and abs((x0 + x1) / 2) > 30:
            continue
        if ln < 2.4:
            continue
        n = 3 if ln >= 7 else (2 if ln >= 4.4 else 1)
        out = (1 if (x0 + x1) / 2 > mid[0] else -1) if along_z else (1 if (z0 + z1) / 2 > mid[1] else -1)
        for k in range(n):
            c = (z0 if along_z else x0) + ln * (k + .5) / n
            if along_z:
                pk.frame_window(g, '+x' if out > 0 else '-x', c - .55, c + .55, y0 + 1.25, y0 + 2.15, x1 if out > 0 else x0, T, frame, F['window'], bars=(1, 1))
                # the inside face too: the room is lit
                pk.frame_window(g, '-x' if out > 0 else '+x', c - .55, c + .55, y0 + 1.25, y0 + 2.15, x0 if out > 0 else x1, T, frame, F['glass'], bars=(1, 1), sill=False)
            else:
                pk.frame_window(g, '+z' if out > 0 else '-z', c - .55, c + .55, y0 + 1.25, y0 + 2.15, z1 if out > 0 else z0, T, frame, F['window'], bars=(1, 1))
                pk.frame_window(g, '-z' if out > 0 else '+z', c - .55, c + .55, y0 + 1.25, y0 + 2.15, z0 if out > 0 else z1, T, frame, F['glass'], bars=(1, 1), sill=False)
    # the roof and its stovepipe (toward the field's side)
    x0, x1, y0, y1, z0, z1 = bx(rb)
    px = mid[0] + (1 if mid[0] < 0 else -1) * ((x1 - x0) / 2 - 1.2)
    roof(g, x0 - .1, x1 + .1, z0 - .1, z1 + .1, y0 + .0, .2, T, seed, pipe_at=(px, mid[1] + (z1 - z0) / 2 - 1.4))
    # snow banked against its windward walls (west and north; never across a door)
    pk.skirt(g, min(bx(b)[0] for b in walls), max(bx(b)[1] for b in walls), min(bx(b)[4] for b in walls), max(bx(b)[5] for b in walls), T, seed, sides='nw' if kind != 'lab' else 'new', h=.22, w=.4)
    # the floor inside: boards (a cabin) or a slab, a hand over the ice
    wx0, wx1 = min(bx(b)[0] for b in walls), max(bx(b)[1] for b in walls)
    wz0, wz1 = min(bx(b)[4] for b in walls), max(bx(b)[5] for b in walls)
    sk.box(g, wx0 + .2, wx1 - .2, .0, .048 if T.i == 0 else .09, wz0 + .2, wz1 - .2, F['timber-z'] if kind == 'cabin' else F['concrete-dark'], T, bevel=0, cell=(1.5, 0, 0)[T.i], skip=('-y',))
    # ceiling boards under it (seen from inside)
    sk.box(g, x0 + .1, x1 - .1, y0 - .02, y0 + .0, z0 + .1, z1 - .1, F['timber-dark'], T, bevel=0, cell=0, skip=('+y',))


def furniture(g, L, T, side):
    for b in L['blocks']:
        i = b['id']
        if b['pos'][0] * side <= 1e-6 or b['role'] != 'static':
            continue
        x0, x1, y0, y1, z0, z1 = bx(b)
        if '-porch' in i:
            # a windbreak: boards on posts, snow along its head
            long_x = (x1 - x0) > (z1 - z0)
            for k, y in enumerate(np.arange(y0 + .1, y1 - .2, .42 if T.i == 0 else 2.4)):
                hh = .36 if T.i == 0 else y1 - y0 - .3
                sk.box(g, x0 + (0 if long_x else .08), x1 - (0 if long_x else .08), y, y + hh, z0 + (.08 if long_x else 0), z1 - (.08 if long_x else 0), F['timber'] if long_x else F['timber-z'], T, bevel=.02, cell=3.0)
            for u in np.linspace((x0 if long_x else z0) + .1, (x1 if long_x else z1) - .1, 4 if long_x else 3):
                if long_x:
                    sk.box(g, u - .1, u + .1, y0, y1, z0, z1, F['timber-dark'], T, bevel=.02, cell=0, skip=('-y',))
                else:
                    sk.box(g, x0, x1, y0, y1, u - .1, u + .1, F['timber-dark'], T, bevel=.02, cell=0, skip=('-y',))
            pk.mound(g, x0 - .02, x1 + .02, z0 - .02, z1 + .02, y1 - .02, .14, F['snow'], T, seed=int(z0 * 7) % 91, steep=.1, bulge=.03, cell=.45)
        elif i.endswith('-screen'):
            planks(g, x0, x1, y0, y1, z0, z1, T, F['timber'], F['timber-z'])
            sk.box(g, x0 - .02, x1 + .02, y0, y0 + .2, z0 - .02, z1 + .02, F['timber-dark'], T, bevel=.02, cell=0, skip=('-y',))
        elif i.endswith('-stove'):
            # a cast-iron stove: a drum on legs, its door glowing, its pipe up through the roof
            cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
            sk.lathe(g, [(.3, .12), (.38, .2), (.4, .8), (.34, 1.0), (.2, y1 - y0)], (cx, y0, cz), F['black'], (12, 8, 6)[T.i], cap1=True)
            for k in range(4):
                a = k * math.pi / 2 + math.pi / 4
                sk.box(g, cx + math.cos(a) * .28 - .04, cx + math.cos(a) * .28 + .04, y0, y0 + .2, cz + math.sin(a) * .28 - .04, cz + math.sin(a) * .28 + .04, F['black'], T, bevel=.01, cell=0)
            for s in (-1, 1):
                g.face(((cx + s * .405, y0 + .34, cz - .16), (cx + s * .405, y0 + .34, cz + .16), (cx + s * .405, y0 + .66, cz + .16), (cx + s * .405, y0 + .66, cz - .16)), F['ember'], normal=(s, 0, 0))
            pk.pipe(g, (cx, y1, cz), (cx, 3.2, cz), .08, F['black'], T, caps=False)
        elif i.endswith('-bunk'):
            sk.box(g, x0, x1, y0, y0 + .5, z0, z1, F['timber-dark'], T, bevel=.02, cell=0, skip=('-y',))
            pk.body(g, x0 + .03, x1 - .03, y0 + .5, y1, z0 + .03, z1 - .03, F['red'], r=.1, T=T)
        elif i.endswith('-bench'):
            sk.box(g, x0, x1, y1 - .1, y1, z0, z1, F['timber'], T, bevel=.02, cell=0)
            sk.box(g, x0 + .05, x1 - .05, y0, y1 - .1, z0 + .05, z1 - .05, F['steel-dark'], T, bevel=.02, cell=0, skip=('-y',))
            if T.detail:
                for k, z in enumerate(np.arange(z0 + .3, z1 - .3, .55)):
                    sk.box(g, x0 + .15, x1 - .15, y1, y1 + .2 + .1 * H(k, 5), z - .14, z + .14, (F['steel'], F['white'], F['orange'])[k % 3], T, bevel=.03, cell=0)
        elif i.endswith('-genset'):
            pk.body(g, x0, x1, y0, y1, z0, z1, F['yellow'], r=.08, T=T, f_top=F['steel-dark'])
            s = 1 if x0 < 0 else -1
            inner = x1 if s > 0 else x0
            if T.detail:
                for k in range(5):
                    sk.box(g, inner + (0 if s > 0 else -.02), inner + (.02 if s > 0 else 0), y0 + .3 + k * .12, y0 + .36 + k * .12, z1 - 1.3, z1 - .3, F['steel-dark'], T, bevel=0, cell=0)
                g.face(((inner + s * .012, y0 + .7, z0 + .3), (inner + s * .012, y0 + .7, z0 + .7), (inner + s * .012, y0 + .95, z0 + .7), (inner + s * .012, y0 + .95, z0 + .3)), F['screen'], normal=(s, 0, 0))


def west(L, T):
    """Everything west of x = 0 (mirrored for the east)."""
    g = Geo()
    B = {b['id']: b for b in L['blocks']}
    shelf_west(g, L, T)
    ridges_west(g, L, T)
    for b in L['blocks']:
        i = b['id']
        if b['pos'][0] >= -1e-6 or b['role'] != 'static':
            continue
        seed = int(abs(b['pos'][0]) * 7 + abs(b['pos'][2]) * 13) % 997
        if i.startswith('polar-spire-'):
            spire(g, b, T, seed)
        elif i.startswith('polar-floe-'):
            x0, x1, y0, y1, z0, z1 = bx(b)
            pk.heaved(g, x0, x1, y0, y1, z0, z1, T, seed, F['ice'], F['ice-deep'], F['snow'])
            pk.mound(g, x0 - .2, x1 + .2, z0 - .2, z1 + .2, y0, .22, F['snow'], T, seed=seed, steep=.3, bulge=.03, cell=.5)
        elif i.startswith('polar-drift-') and i.endswith('-crest'):
            drift(g, B[i[:-6] + '-apron'], b, T, seed)
        elif i.startswith('polar-tank-'):
            tank(g, b, T, seed)
            pk.skirt(g, *[bx(b)[k] for k in (0, 1, 4, 5)], T, seed, sides='nw')
        elif i.startswith('polar-snowcat-') and not i.endswith('-blade'):
            snowcat(g, b, B[i + '-blade'], T)
            pk.skirt(g, *[bx(b)[k] for k in (0, 1, 4, 5)], T, seed, sides='sw', h=.2)
        elif i.startswith('polar-hut-'):
            hut(g, b, T, seed)
            pk.skirt(g, *[bx(b)[k] for k in (0, 1, 4, 5)], T, seed, sides='sw')
        elif i.startswith('polar-stack-'):
            stack(g, b, T, seed)
            pk.skirt(g, *[bx(b)[k] for k in (0, 1, 4, 5)], T, seed, sides='nsew')
        elif i.startswith('polar-mast-'):
            mast(g, b, T, seed)
        elif i.startswith('polar-sled-'):
            sled(g, b, T, seed)
    building(g, L, T, 'polar-cabin-a-', 'cabin')
    building(g, L, T, 'polar-lab-w-', 'lab')
    building(g, L, T, 'polar-shed-w-', 'shed')
    furniture(g, L, T, -1)
    for h in L['hazards']:
        if h['kind'] == 'vent' and h['a'][0] < 0:
            vent(g, h, T)
    # strings of lamps over the yard (every lamp more than 2 m over what is under it): from the cabin's eaves to the weather
    # mast and to the shed's, from the mast to the lab's
    cab, lab, shed, m = bx(B['polar-cabin-a-roof']), bx(B['polar-lab-w-roof']), bx(B['polar-shed-w-roof']), bx(B['polar-mast-w'])
    mast_at = ((m[0] + m[1]) / 2 - .45, 6.2, (m[4] + m[5]) / 2)
    pk.festoon(g, (cab[1] + .16, cab[3] + .2, cab[5] - .5), mast_at, 1.0, T, F['lamp'])
    pk.festoon(g, mast_at, (lab[1] + .16, lab[3] + .2, lab[5] - .4), .9, T, F['lamp'])
    pk.festoon(g, (cab[1] + .16, cab[3] + .2, cab[4] + .5), (shed[1] + .16, shed[3] + .2, shed[5] - .4), .5, T, F['lamp'])
    return g


def static_scene(L, T):
    import polar_ship
    w = west(L, T)
    g = Geo()
    g.extend(w)
    g.extend(pk.mirror_x(w))
    shelf_centre(g, L, T)
    for b in L['blocks']:
        if abs(b['pos'][0]) < 1e-6 and b['id'].startswith('polar-spire-'):
            spire(g, b, T, 77)
    polar_ship.build(g, L, T)
    return g


VIEWS = [('wide-sw', (-40, 26, -40), (0, 2, 0), 60), ('wide-ne', (36, 24, 36), (0, 2, -4), 60), ('top', (0, 70, -.01), (0, 0, 0), 55), ('cabin-a', (-16, 1.8, -1), (-27, 2, 1), 70),
         ('channel', (0, 1.8, -3), (0, 4, 14), 70), ('bow', (-9, 1.8, 3), (0, 4, 13), 65), ('deck', (0, 5, 16), (0, 9, -16), 70), ('deck-n', (0, 4.9, 10), (0, 7, 24), 70),
         ('terrace', (0, 4.2, -14.6), (0, 8, 20), 70), ('obs', (-14, 1.8, -8), (0, 6, -17), 65), ('pedestal', (6, 4.2, -15), (0, 5.5, -17), 70), ('shelf', (-10, 1.8, -23.5), (6, 2, -23), 70),
         ('lab', (-14, 1.8, 22), (-26, 2, 17), 70), ('inside', (-29.5, 1.7, -4.5), (-24, 1.5, 2), 75), ('field', (-12, 1.8, 2), (-18, 1.6, 10), 70), ('spire', (-11, 1.6, -3.5), (-8.4, 1.8, 0), 60),
         ('snowcat', (-16, 1.8, -3), (-12, 1.2, -7), 60), ('ridge', (-22, 1.8, 16), (-32, 3, 8), 70), ('ship-side', (-22, 4, 30), (0, 6, 30), 60), ('cliff', (10, 3, -40), (0, 0, -25), 60), ('dish', (7, 14, -5), (0, 12, -16), 60), ('dish-back', (-8, 9, -27), (0, 12, -17), 60),
         ('thin', (-1.5, 2.2, -1.5), (.5, 0, 4), 70), ('vent', (-8, 1.6, -9.5), (-5.2, .2, -11.6), 55), ('balloon', (-14, 22, 4), (-20, 22, 10), 60), ('far', (0, 30, 60), (0, 20, 600), 70), ('sea', (0, 6, -20), (20, 10, -300), 70)]


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
    ap.add_argument('--static-only', action='store_true')
    ap.add_argument('--stats', action='store_true')
    ap.add_argument('--own-mid', action='store_true', help='build a Mid tier of its own for the static scene (else the Mid props share the Fine meshes)')
    args = ap.parse_args(argv)
    L = sk.load_layout(args.layout)
    for pre in ('polar-cabin-a-', 'polar-lab-w-', 'polar-shed-w-'):
        ws = [bx(b) for b in L['blocks'] if b['id'].startswith(pre) and '-wall' in b['id']]
        ROOMS.append((min(-w[1] for w in ws) - .5, max(-w[0] for w in ws) + .5, min(w[4] for w in ws) - .5, max(w[5] for w in ws) + .5))
    pk.finishes()
    import polar_props as props
    sb = sk.StageBank()
    chunks = sk.Chunks(12.0, (-36.0, -36.0), (-.9, 9.0))
    t0 = time.time()
    fine = {}
    ident = [1.0, 0, 0, 0, 0, 1.0, 0, 0, 0, 0, 1.0, 0]
    for ti in [int(x) for x in args.tiers.split(',')]:
        T = sk.Tier(ti)
        t = time.time()
        if ti == 1 and fine and not args.own_mid and not (args.preview and args.preview_tier == 1):
            # the Mid props share the Fine meshes (nothing is stored twice; the Fine tier draws out to the Mid reach)
            for cid, mi in fine.items():
                sb.share('static/%d/1' % cid, 'static', mi)
            if not args.static_only:
                props.bank_props(sb, L, T, args.no_ao)
            continue
        g = static_scene(L, T)
        print('  coplanar: %d overlapping pairs, %.2f m2, %d faces stacked, %.0f mm at most' % sk.lift_coplanar(g, eps=sk.NEAR_TIER[ti], lift=sk.LIFT_TIER[ti]))
        print('tier %s: static %d faces, %.1f s' % (T.name, g.faces, time.time() - t))
        ao = None
        if not args.no_ao:
            # an open-air stage under a soft sky: the occlusion reaches a metre and is eased
            ao = 1.0 - (1.0 - sk.bake_ao([g], distance=(1.1, 1.1, 1.3)[ti], samples=(32, 16, 12)[ti])[0]) * .75
        bk = sk.Bake(g, ao, floors=FLOORS, seed=3)
        ids = chunks.ids(g)
        for cid in np.unique(ids):
            om = sk.to_mesh('static/%d/%d' % (cid, ti), g, bk, ids == cid)
            if om is None:
                continue
            mi = sb.mesh(om)
            sb.bank.prop('static/%d/%d' % (cid, ti), 'static', [(mi, ident)])
            st = sb.stats.setdefault(('static', str(ti)), [0, 0, 0])
            st[0] += 1
            st[1] += sum(len(x) for x in om.submeshes) // 3
            st[2] += len(om.positions)
            if ti == 0:
                fine[int(cid)] = mi
        if args.stats:
            P, N, S, Fi = g.arrays()
            names = {v: k for k, v in F.items()}
            by = {}
            for k, ff in zip(S, Fi):
                by[names.get(int(ff), '?')] = by.get(names.get(int(ff), '?'), 0) + int(k)
            print('  corners by finish:', ', '.join('%s %d' % (k, v) for k, v in sorted(by.items(), key=lambda q: -q[1])[:16]))
        if args.preview and ti == args.preview_tier:
            views = [v for v in VIEWS if not args.views or v[0] in args.views.split(',')]
            extra = []
            for name, fg in props.far(L):
                extra.append((fg, sk.Bake(fg, None, floors=(), seed=5)))
            # the rig's props where they stand at a moment, and in a row well clear of the stage
            row = Geo()
            for k, (name, pg, rest) in enumerate(props.items(L, T)):
                row.extend(pg.transformed([1, 0, 0, rest[0], 0, 1, 0, rest[1], 0, 0, 1, rest[2]]))
            for k, (bid, pg) in enumerate(props.blocks(L, T)):
                p = [b for b in L['blocks'] if b['id'] == bid][0]['pos']
                row.extend(pg.transformed([1, 0, 0, p[0], 0, 1, 0, p[1], 0, 0, 1, p[2]]))
            extra.append((row, sk.Bake(row, None, floors=(), seed=7)))
            sk.preview([(g, bk)] + extra, views, args.preview)
        if not args.static_only:
            props.bank_props(sb, L, T, args.no_ao)
        del g
    if not args.static_only:
        props.bank_far(sb, L)
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
