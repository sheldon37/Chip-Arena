"""Top Dog Emporium's art (docs/arenas/EMPORIUM.md "Stage art", docs/arenas/STAGE-ART.md): the hot dog palace modelled in Blender
on the map's own blocks and its Core kit's parts, into the stage bank Resources/ChipArenaStageEmporium (stagekit.py's format and
bake). Runtime NativeStageArt draws it in place of the boxes; without the bank the box kit draws as before.

usage (from unity/ChipArenaConversion):
  dotnet run --project Tests/CoreTests.csproj -c Release -- --stage-layout emporium LAYOUT.json
  blender --background --factory-startup --python tools/stages/emporium_art.py -- --layout LAYOUT.json --out ASSETS/LocalAssets
          [--preview DIR] [--tiers 0,1,2] [--no-ao] [--static-only]

This file: the hall (the tiled floor as a mosaic, the roller beds, the walls' wainscot, rails, frieze, cornice and arched
windows, the glazed roof's arched trusses, the pendant lamps) and the big fixtures (the wiener ovens with their portals,
rotisserie windows and striped awnings, the kitchens' walls, fry stations and pantry racks, the Big Dog's plinth, the bun presses'
frames, the giant sauce bottles). emporium_fit.py: the furniture (booths, counters and stools, soda fountains, jukeboxes, pumps,
kiosks, fridges, the bun bridges, stairs, balconies). emporium_props.py: the rigs and the breakable blocks. Everything else the
kit carries is rebuilt from its own parts (emporium_kit.kit_part): tubes round, decals flat, plates chamfered.
"""
import argparse
import math
import os
import sys
import time

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stagekit as sk  # noqa: E402
import emporium_kit as ek  # noqa: E402
from stagekit import Geo, box, lathe  # noqa: E402
from emporium_kit import Face, ext, kmat, mat, pfin, kit_part, capsule, ellipsoid, loft, part_round, tube, disc, rrect, KINDS  # noqa: E402

BANK = 'ChipArenaStageEmporium'
BL = {}
RIG = {}
Y = (0.0, 1.0, 0.0)


def kn(p):
    return KINDS[p['k']]


def thin(p):
    return min(p['h']) <= .0125


def span(fc, p):
    """A part on a face: its centre (u, v), half extents (along u, up v) and its outer plane's d."""
    c, h = p['c'], p['h']
    u = c[2] if fc.side < 2 else c[0]
    d = (c[0] - fc.o[0]) * fc.N[0] + (c[2] - fc.o[2]) * fc.N[2]
    hu, hn = (h[2], h[0]) if fc.side < 2 else (h[0], h[2])
    return u, c[1], hu, h[1], d + hn


def mould(g, fc, u0, u1, profile, f):
    """A moulding along a face: profile [(d, v)] bottom to top, shaded round."""
    n = len(profile)
    nr = []
    for i in range(n):
        a, b = profile[max(0, i - 1)], profile[min(n - 1, i + 1)]
        td, tv = b[0] - a[0], b[1] - a[1]
        l = math.hypot(td, tv) or 1.0
        nd, nv = tv / l, -td / l
        nr.append((fc.N[0] * nd, nv, fc.N[2] * nd))
    for i in range(n - 1):
        (d0, v0), (d1, v1) = profile[i], profile[i + 1]
        g.face((fc.P(u0, v0, d0), fc.P(u1, v0, d0), fc.P(u1, v1, d1), fc.P(u0, v1, d1)), f, normals=(nr[i], nr[i], nr[i + 1], nr[i + 1]))


def arch(z):
    hz = RIG['roofHalfZ']
    u = min(1.0, abs(z) / hz)
    return RIG['eaves'] + (RIG['ridge'] - RIG['eaves']) * math.sqrt(max(0.0, 1 - u * u))


# ---------------------------------------------------------------------------------------------------------------------------
# The floor: a mosaic (no overlapping decals for the big fields), the roller beds, the zones
# ---------------------------------------------------------------------------------------------------------------------------
def floor(g, L, T, parts):
    x0, x1, _, _, z0, z1 = ext(BL['emporium-floor'])
    lanes = [h for h in L['hazards'] if h['kind'] == 'maglev']
    inner = min(abs(h['center'][2]) - h['size'][2] / 2 for h in lanes)
    outer = max(abs(h['center'][2]) + h['size'][2] / 2 for h in lanes)
    lx = max(h['size'][0] / 2 for h in lanes)
    cell = (1.5, 3.0, 6.0)[T.i]
    quarry, bed, teal = kmat('quarry'), mat('lane-bed', 0x24212A, smooth=.5, metal=.3, dirt=.3), kmat('teal')
    tile = mat('floor-tile', 0xEFE2CA, sk.DETAIL_TILE, smooth=.72, dirt=.3, param=.6)
    white, black = mat('checker-white', 0xF6EEDD, smooth=.84, dirt=.25), mat('checker-black', 0x231F28, smooth=.86, dirt=.2)
    grout = mat('grout', 0x7A7068, smooth=.2, dirt=.4)
    sk.grid_face(g, (x0, 0, -inner), (x1 - x0, 0, 0), (0, 0, 2 * inner), 0, 0, quarry, Y, cell)
    for s in (-1, 1):
        za, zb = (inner, outer) if s > 0 else (-outer, -inner)
        sk.grid_face(g, (-lx, 0, za), (2 * lx, 0, 0), (0, 0, zb - za), 0, 0, bed, Y, cell)
        for xa, xb in ((x0, -lx), (lx, x1)):
            sk.grid_face(g, (xa, 0, za), (xb - xa, 0, 0), (0, 0, zb - za), 0, 0, tile, Y, cell)
        za, zb = (outer, outer + .4) if s > 0 else (-outer - .4, -outer)
        sk.grid_face(g, (x0, 0, za), (x1 - x0, 0, 0), (0, 0, zb - za), 0, 0, teal, Y, cell * 2)
        # the diner's checkerboard: every tile its own face, a hair apart over the grout
        zs, sq, gap = outer + .4, 1.2, (.006 if T.i < 2 else 0.0)
        if T.i < 2:
            g.face(((x0, -.004, s * zs), (x1, -.004, s * zs), (x1, -.004, s * z1), (x0, -.004, s * z1)), grout, Y)
        j = 0
        while zs + j * sq < z1 - .01:
            i = 0
            while -29.0 + i * sq < 29.0 - .01:
                xa, xb = -29.0 + i * sq, min(29.4, -29.0 + (i + 1) * sq)
                if i == 0:
                    xa = x0
                ca, cb = zs + j * sq, min(z1, zs + (j + 1) * sq)
                f = black if ((i + j) & 1) == 1 else white
                a, b = (ca, cb) if s > 0 else (-cb, -ca)
                g.face(((xa + gap, 0, a + gap), (xb - gap, 0, a + gap), (xb - gap, 0, b - gap), (xa + gap, 0, b - gap)), f, Y)
                i += 1
            j += 1
    # the geysers' nozzle rings
    for h in L['hazards']:
        if h['kind'] == 'vent':
            a = h['a']
            lathe(g, [(.5, 0), (.5, .03), (.56, .055), (.62, .03), (.62, 0)], (a[0], a[1], a[2]), kmat('chrome'), T.round + 4)
            lathe(g, [(.92, 0), (.92, .02), (.97, .04), (1.02, .02), (1.02, 0)], (a[0], a[1], a[2]), kmat('chrome'), T.round + 6)
    seed = 0
    for p in parts:
        name = kn(p)
        c, h = p['c'], p['h']
        seed += 1
        if name in ('checker', 'quarry', 'teal') or (name == 'dark' and thin(p)):
            continue
        if name == 'roller':
            part_round(g, (c[0], 0, c[2] - h[2]), (c[0], 0, c[2] + h[2]), .05, pfin(p), (1, 0, 0), Y, 0, 180, 3 if T.i == 0 else 2)
        elif name in ('ketchup', 'mustard') and thin(p):
            r = max(h[0], h[2])
            disc(g, (c[0], c[1] + h[1] + (.002 if r > .6 else .004), c[2]), r * (1.12 if r > .6 else 1.25), pfin(p), (16, 10, 6)[T.i] if r > .6 else (9, 6, 5)[T.i], Y, .16, seed, .012)
        elif name == 'chrome' and abs(h[1] - .025) < .002 and h[0] > 2:
            part_round(g, (c[0] - h[0], 0, c[2]), (c[0] + h[0], 0, c[2]), .055, pfin(p), (0, 0, 1), Y, 0, 180, 3 if T.i == 0 else 2)
        else:
            kit_part(g, p, T, None)


# ---------------------------------------------------------------------------------------------------------------------------
# The hall's walls
# ---------------------------------------------------------------------------------------------------------------------------
def window(g, fc, p, T):
    u, v, hu, hv, d = span(fc, p)
    u0, u1, v0, v1 = u - hu, u + hu, v - hv, v + hv
    rise = min(hu * .55, 1.0)
    spring = v1 + .2 - rise
    n = (16, 10, 6)[T.i]
    glass, dark = pfin(p), kmat('dark')

    def top(x):
        return spring + sk.arch_height(hu, rise, min(x - u0, u1 - x))
    for i in range(n):
        xa, xb = u0 + (u1 - u0) * i / n, u0 + (u1 - u0) * (i + 1) / n
        g.face((fc.P(xa, v0, .03), fc.P(xb, v0, .03), fc.P(xb, top(xb), .03), fc.P(xa, top(xa), .03)), glass, fc.N)
    path = [(u0, v0, .05)] + [(u0 + (u1 - u0) * i / n, top(u0 + (u1 - u0) * i / n), .05) for i in range(n + 1)] + [(u1, v0, .05)]
    sk.tube(g, fc.path(path), .075, dark, T.small, closed=True)
    m = 3 if hu < 2 else 5
    sp = .75 if hu < 2 else .92
    for k in range(m):
        x = u + (k - (m - 1) / 2) * sp
        sk.cylinder(g, fc.P(x, v0, .05), fc.P(x, top(x), .05), .035, dark, T.small, caps=False)
    sk.cylinder(g, fc.P(u0, v, .05), fc.P(u1, v, .05), .035, dark, T.small, caps=False)
    fc.slab(g, u0 - .12, u1 + .12, v0 - .16, v0 - .06, 0, .16, kmat('cream'), T, .02)


def relief(g, p, n, T, f=None, squash=.5):
    """A plate as a rounded relief: a capsule along its long axis, flattened along n."""
    ax, half, others = ek.long_axis(p)
    r = max(others)
    f = pfin(p) if f is None else f
    c = sk._add(p['c'], n, -min(others))
    k = max(0.0, half - r)
    capsule(g, sk._add(c, ax, -k), sk._add(c, ax, k) if k > 0 else sk._add(c, ax, .001), r, f, (8, 6, 4)[T.i], caps=2 if T.i == 0 else 1, squash=(1.0, squash * 2 * min(others) / r if r > 0 else 1.0), up=n)


def wall(g, b, parts, T):
    cx, cz = b['pos'][0], b['pos'][2]
    side = (3 if cz > 0 else 2) if abs(cz) > abs(cx) else (1 if cx > 0 else 0)
    fc = Face(b, side)
    u0, u1 = fc.u0, fc.u1
    cell = (1.5, 3.0, 6.0)[T.i]
    chrome, dark, cream = kmat('chrome'), kmat('dark'), kmat('cream')
    walltile = mat('wall-tile', 0xF2EDE2, sk.DETAIL_TILE, smooth=.62, dirt=.35, param=.3)
    k = 4 if T.i == 0 else 2
    fc.quad(g, u0, u1, 0, .13, .03, dark)
    part_round(g, fc.P(u0, .13, 0), fc.P(u1, .13, 0), .03, dark, fc.N, Y, 0, 90, 2)
    fc.quad(g, u0, u1, .16, 1.25, .012, kmat('teal'), cell)
    fc.bead(g, u0, u1, 1.295, .05, chrome, .012, k)
    fc.quad(g, u0, u1, 1.34, 6.8, 0, walltile, cell)
    fc.bead(g, u0, u1, 6.84, .045, chrome, 0, k)
    fc.quad(g, u0, u1, 6.88, 7.78, .012, kmat('pink'), cell * 2)
    mould(g, fc, u0, u1, [(.012, 7.78), (.03, 7.8), (.05, 7.86), (.1, 7.93), (.18, 7.96), (.2, 8.0)], cream)
    fc.quad(g, u0, u1, 8.0, 8.001, .2, cream)
    g.face((fc.P(u0, 8.0, .2), fc.P(u1, 8.0, .2), fc.P(u1, 8.0, -.4), fc.P(u0, 8.0, -.4)), cream, Y)
    if T.i < 2:
        x = u0 + 1.2
        while x < u1 - 1:
            fc.stile(g, x, 6.92, 7.76, .06, cream, .012, 3 if T.i == 0 else 2, .035)
            x += 2.4
    for p in parts:
        fam = p['f']
        name = kn(p)
        if fam == 'wall':
            if name == 'glass':
                window(g, fc, p, T)
            elif name == 'tube':
                kit_part(g, p, T, b)
            continue
        if fam == 'wall:poster':
            u, v, hu, hv, d = span(fc, p)
            if name == 'chrome':
                kit_part(g, p, T, b)
                fc.frame(g, u - hu, u + hu, v - hv, v + hv, d, .03, chrome, T.small, .06, 2)
            elif name in ('bun', 'sausage') and T.i < 2:
                relief(g, p, fc.N, T)
            elif name == 'mustard' and T.i == 0:
                pts = [fc.P(u - hu + 2 * hu * i / 10, v + (.022 if i % 2 else -.022), d + .012) for i in range(11)]
                tube(g, sk.catmull(pts, 3), .014, pfin(p), 4)
            else:
                kit_part(g, p, T, b)
            continue
        kit_part(g, p, T, b)


# ---------------------------------------------------------------------------------------------------------------------------
# The glazed roof: arched trusses, purlins, glazing bars; the pendant lamps
# ---------------------------------------------------------------------------------------------------------------------------
def roof(g, T):
    hz, hx, eaves, ridge = RIG['roofHalfZ'], RIG['roofHalfX'], RIG['eaves'], RIG['ridge']
    iron = kmat('iron')
    n = (40, 20, 10)[T.i]

    def at(t, lift=0.0, pull=1.0):
        return (hz * math.cos(t) * pull, eaves + (ridge - eaves) * math.sin(t) + lift)
    xs = []
    x = -hx
    while x <= hx + 1e-6:
        xs.append(x)
        x += 4.0
    if xs[-1] < hx - .5:
        xs.append(hx)
    for x in xs:
        low = [(x, at(math.pi * i / n)[1], at(math.pi * i / n)[0]) for i in range(n + 1)]
        up = [(x, at(math.pi * i / n, .55, .98)[1], at(math.pi * i / n, .55, .98)[0]) for i in range(n + 1)]
        sk.sweep(g, sk.rect_section(.18, .3, .04 if T.i == 0 else 0), low, iron, up=(1, 0, 0), caps=False)
        sk.tube(g, up, .06, iron, T.small, caps=False)
        if T.i < 2:
            m = 20
            for k in range(m):
                t0, t1 = math.pi * (k + .0) / m, math.pi * (k + 1.0) / m
                a, b = at(t0), at(t1, .55, .98)
                c, d = at(t1), at(t1, .55, .98)
                if k % 2:
                    a, b = at(t0, .55, .98), at(t1)
                sk.cylinder(g, (x, a[1], a[0]), (x, b[1], b[0]), .03, iron, 4, caps=False)
                if k < m - 1 and T.i == 0:
                    sk.cylinder(g, (x, c[1], c[0]), (x, d[1], d[0]), .03, iron, 4, caps=False)
    for z in (-21.0, -17.0, -12.6, -8.4, -4.2, 0.0, 4.2, 8.4, 12.6, 17.0, 21.0):
        y = arch(z)
        sk.sweep(g, sk.rect_section(.12, .14), [(-hx, y, z), (hx, y, z)], iron, up=(0, 1, 0))
    for s in (-1, 1):
        box(g, -hx, hx, eaves - .25, eaves + .05, s * hz - (.4 if s > 0 else 0), s * hz + (.4 if s < 0 else 0), iron, T, .03, cell=0)
    if T.i < 2:
        # the glazing bars between the trusses: thin ribbons following the vault (their stripes fall across the hall)
        m = n // 2
        x = -hx + 2.0
        while x < hx:
            for i in range(m):
                t0, t1 = math.pi * i / m, math.pi * (i + 1) / m
                a, b = at(t0, .08), at(t1, .08)
                for w, nrm in ((1, (0, -1, 0)), (-1, (0, 1, 0))):
                    g.face(((x - .04, a[1], a[0]), (x + .04, a[1], a[0]), (x + .04, b[1], b[0]), (x - .04, b[1], b[0])), iron, nrm)
            x += 4.0


def pendants(g, parts, T):
    red, chrome, dark = kmat('red'), kmat('chrome'), kmat('dark')
    for p in parts:
        if kn(p) != 'tube':
            continue
        x, y, z = p['c'][0], p['c'][1] + .04, p['c'][2]
        lathe(g, [(.36, .02), (.355, .05), (.3, .15), (.2, .29), (.1, .43), (.07, .5)], (x, y, z), red, T.round)
        lathe(g, [(.07, .5), (.075, .6), (.035, .64), (.0, .65)], (x, y, z), chrome, T.small)
        lathe(g, [(.0, .3), (.2, .26), (.34, .04)], (x, y, z), mat('shade-inside', 0xFFF1D6, smooth=.3, dirt=0, glow=.9), T.round)
        ellipsoid(g, (x, y - .02, z), .1, .12, .1, pfin(p), max(5, T.small), 3)
        sk.cylinder(g, (x, y + .64, z), (x, arch(z), z), .012, dark, 3, caps=False)


# ---------------------------------------------------------------------------------------------------------------------------
# Awnings
# ---------------------------------------------------------------------------------------------------------------------------
def awning(g, fc, u0, u1, top, drop, out, T, fa=None, fb=None, stripe=.42, valance=.2):
    """A striped awning on a face: from the wall at `top` sloping down `drop` and `out`, a scalloped valance hanging from its edge;
    both sides drawn (it is seen from below)."""
    fa = fa or mat('awning-red', 0xD8322A, sk.DETAIL_CLOTH, smooth=.25, dirt=.3)
    fb = fb or mat('awning-white', 0xFBF4E6, sk.DETAIL_CLOTH, smooth=.25, dirt=.3)
    n = max(1, int(round((u1 - u0) / stripe)))
    w = (u1 - u0) / n
    l = math.hypot(drop, out)
    nt = (fc.N[0] * drop / l, out / l, fc.N[2] * drop / l)
    nb = (-nt[0], -nt[1], -nt[2])
    seg = (5, 3, 1)[T.i]
    for i in range(n):
        f = fa if i % 2 == 0 else fb
        a, b = u0 + i * w, u0 + (i + 1) * w
        A, B, C, D = fc.P(a, top, 0), fc.P(b, top, 0), fc.P(b, top - drop, out), fc.P(a, top - drop, out)
        g.face((A, B, C, D), f, nt)
        g.face((A, B, C, D), f, nb)
        # the valance: a scallop
        pts = [fc.P(a, top - drop, out), fc.P(b, top - drop, out)]
        for k in range(seg + 1):
            ang = math.pi * k / seg
            pts.append(fc.P((a + b) / 2 + math.cos(ang) * w / 2, top - drop - valance * (.45 + .55 * math.sin(ang)), out))
        g.face(pts, f, fc.N)
        g.face(pts, f, (-fc.N[0], 0, -fc.N[2]))
    for u in (u0, u1):
        tri = (fc.P(u, top, 0), fc.P(u, top - drop, out), fc.P(u, top - drop, 0))
        s = -1 if u == u0 else 1
        g.face(tri, fa, (fc.U[0] * s, 0, fc.U[2] * s))
        g.face(tri, fa, (-fc.U[0] * s, 0, -fc.U[2] * s))
    sk.cylinder(g, fc.P(u0, top - drop, out), fc.P(u1, top - drop, out), .02, kmat('chrome'), 4, caps=False)


# ---------------------------------------------------------------------------------------------------------------------------
# The wiener ovens
# ---------------------------------------------------------------------------------------------------------------------------
def oven(g, b, parts, T):
    x0, x1, y0, y1, z0, z1 = ext(b)
    cx, cz = b['pos'][0], b['pos'][2]
    lane = 0 if cx < 0 else 1
    steel = mat('stainless', 0xD8DEE6, smooth=.72, metal=.9, dirt=.2)
    chrome, red = kmat('chrome'), kmat('red')
    box(g, x0, x1, y0, y1, z0, z1, steel, T, .09, cell=(1.2, 3, 6)[T.i], skip=('-y',))
    fl = Face(b, lane)
    # the portal's moulding round the flap
    r, hw, top = .2, 1.97, 2.33
    path = [(cz - hw, 0, .03), (cz - hw, top - r, .03)] + [(cz - hw + r - r * math.cos(a), top - r + r * math.sin(a), .03) for a in (math.pi / 6, math.pi / 3, math.pi / 2)] + \
        [(cz + hw - r + r * math.sin(a), top - r + r * math.cos(a), .03) for a in (0, math.pi / 6, math.pi / 3, math.pi / 2)] + [(cz + hw, 0, .03)]
    sk.tube(g, fl.path(path), .13, red, T.small + 1, caps=True)
    for s in (2, 3):
        fs = Face(b, s)
        awning(g, fs, fs.u0 + .15, fs.u1 - .15, 3.62, .42, .62, T)
    for p in parts:
        name, fam = kn(p), p['f']
        c, h = p['c'], p['h']
        if fam != 'oven':
            kit_part(g, p, T, b)
            continue
        if c[1] > y1 + .01:
            if name == 'chrome' and h[1] > .5:
                lathe(g, [(.3, 0), (.3, 2 * h[1] - .1), (.34, 2 * h[1] - .08), (.34, 2 * h[1])], (c[0], y1, c[2]), chrome, T.round)
                continue
            if name == 'red' and h[1] < .1:
                lathe(g, [(.5, 0), (.46, .06), (.0, .34)], (c[0], c[1] - h[1] + .12, c[2]), red, T.round, cap0=True)
                for a in range(4):
                    ang = math.pi / 4 + a * math.pi / 2
                    sk.cylinder(g, (c[0] + .28 * math.cos(ang), c[1] - h[1], c[2] + .28 * math.sin(ang)), (c[0] + .4 * math.cos(ang), c[1] - h[1] + .14, c[2] + .4 * math.sin(ang)), .02, chrome, 3, caps=False)
                continue
            kit_part(g, p, T, b)
            continue
        if name == 'red' and (abs(c[0] - (x1 if lane == 0 else x0)) < .1 and c[1] < 2.5):
            continue                                        # the portal's flat frame: the moulding stands there
        if name == 'red' and abs(c[1] - 3.3) < .05:
            continue                                        # the band the awning replaces
        side = None
        for s in (2, 3):
            fs = Face(b, s)
            if abs(span(fs, p)[4]) < .12 and abs(c[2] - (z1 if s == 2 else z0)) < .15:
                side = fs
        if side is not None and name == 'glass':
            u, v, hu, hv, d = span(side, p)
            kit_part(g, p, T, b)
            side.frame(g, u - hu - .06, u + hu + .06, v - hv - .06, v + hv + .06, d + .03, .06, chrome, T.small, .22, 3)
            if T.i < 2:
                for vv in (v - hv * .45, v + hv * .5):
                    sk.cylinder(g, side.P(u - hu, vv, d + .05), side.P(u + hu, vv, d + .05), .018, chrome, 4, caps=False)
                    for k in range(4):
                        uu = u - hu + (k + .5) * 2 * hu / 4
                        capsule(g, side.P(uu - .17, vv, d + .05), side.P(uu + .17, vv, d + .05), .075, kmat('sausage'), (7, 5, 4)[T.i], caps=1)
            continue
        if side is not None and name == 'chrome' and h[1] > .7:
            continue                                        # the window's flat bezel
        if side is not None and name == 'dark' and h[1] < .05 and max(h[0], h[2]) > .9:
            continue                                        # the flat bar across the glass
        if side is not None and name == 'dark' and abs(h[1] - .07) < .01:
            u, v, hu, hv, d = span(side, p)
            lathe(g, [(.085, 0), (.085, .025), (.06, .05), (.035, .06), (0, .062)], side.P(u, v, 0), chrome, T.small + 2, axis=side.N)
            continue
        if name == 'yellow' and not thin(p):
            kit_part(g, p, T, b)
            for s in (lane, 1 - lane):
                fs = Face(b, s)
                u, v, hu, hv, d = span(fs, p)
                if abs(d) < .1:
                    fs.frame(g, u - hu, u + hu, v - hv, v + hv, d, .028, chrome, T.small, .05, 2)
            continue
        kit_part(g, p, T, b)


# ---------------------------------------------------------------------------------------------------------------------------
# The kitchens: walls, fry stations, pantry racks
# ---------------------------------------------------------------------------------------------------------------------------
def kwall(g, b, parts, T):
    x0, x1, y0, y1, z0, z1 = ext(b)
    box(g, x0, x1, y0, y1 - .02, z0, z1, mat('kitchen-tile', 0xEFE4D0, sk.DETAIL_TILE, smooth=.7, dirt=.3, param=.15), T, .012, cell=(1.2, 3, 6)[T.i], skip=('-y',))
    part_round(g, ((x0 + x1) / 2, y1 - .02, z0), ((x0 + x1) / 2, y1 - .02, z1), (x1 - x0) / 2 + .02, kmat('chrome'), (1, 0, 0), Y, 0, 180, 4 if T.i == 0 else 2, .07)
    for p in parts:
        name = kn(p)
        if p['f'] == 'kwall' and name in ('red', 'yellow') and thin(p):
            kit_part(g, p, T, b, mat('team-tile-%06x' % p['col'], p['col'], sk.DETAIL_TILE, smooth=.78, dirt=.25, param=.15))
        elif p['f'] == 'kwall' and name == 'chrome' and p['h'][1] > 1.5:
            # the door edge: a chrome bullnose up the wall's end
            s = 3 if b['pos'][2] > 0 else 2
            z = z0 if s == 3 else z1
            part_round(g, ((x0 + x1) / 2, 0, z), ((x0 + x1) / 2, y1 - .02, z), (x1 - x0) / 2 + .015, kmat('chrome'), (1, 0, 0), (0, 0, -1 if s == 3 else 1), 0, 180, 4 if T.i == 0 else 2, .06)
        else:
            kit_part(g, p, T, b)


def fryer(g, b, parts, T):
    x0, x1, y0, y1, z0, z1 = ext(b)
    steel = mat('stainless', 0xD8DEE6, smooth=.72, metal=.9, dirt=.2)
    box(g, x0, x1, y0, y1, z0, z1, mat('fryer', 0xCDD4DC, smooth=.7, metal=.85, dirt=.25), T, .04, cell=(1.2, 3, 6)[T.i], skip=('-y',))
    front = 0 if b['pos'][0] < 0 else 1
    fc = Face(b, front)
    mould(g, fc, fc.u0, fc.u1, [(0, 2.58), (.46, 2.6), (.48, 2.64), (.48, 2.8), (.22, 3.98), (0, 4.0)], steel)
    for u in (fc.u0, fc.u1):
        g.face((fc.P(u, 2.58, 0), fc.P(u, 2.6, .46), fc.P(u, 2.8, .48), fc.P(u, 3.98, .22), fc.P(u, 4.0, 0)), steel, (fc.U[0] * (-1 if u == fc.u0 else 1), 0, fc.U[2] * (-1 if u == fc.u0 else 1)))
    g.face((fc.P(fc.u0, 2.58, 0), fc.P(fc.u1, 2.58, 0), fc.P(fc.u1, 2.6, .46), fc.P(fc.u0, 2.6, .46)), mat('hood-under', 0x3A3F48, smooth=.4, metal=.6), (0, -1, 0))
    for p in parts:
        name = kn(p)
        u, v, hu, hv, d = span(fc, p)
        if p['f'] == 'fryer' and name == 'chrome' and p['h'][1] > .6 and v > 3:
            continue                                        # the flat hood face
        if p['f'] == 'fryer:neon':
            # the hood's face leans back: the letters follow it
            q = dict(p)
            lean = (v - 2.8) / 1.18 * .26
            q['c'] = [p['c'][0] + fc.N[0] * (.28 - lean), p['c'][1], p['c'][2] + fc.N[2] * (.28 - lean)]
            kit_part(g, q, T, b)
            continue
        if p['f'] == 'fryer' and name == 'chrome' and abs(hu - .03) < .005 and hv > .1:
            capsule(g, fc.P(u, v - hv, d - .03), fc.P(u, v + hv, d + .02), .025, kmat('chrome'), T.small, caps=1)
            continue
        kit_part(g, p, T, b)


GOODS = ('bun', 'ketchup', 'mustard', 'relish', 'onion', 'pickle')


def shelf(g, b, parts, T):
    x0, x1, y0, y1, z0, z1 = ext(b)
    box(g, x0, x1, y0, y1, z0, z1, mat('pantry', 0x2E4656, smooth=.45, metal=.4, dirt=.3), T, .02, cell=(1.5, 3, 6)[T.i], skip=('-y',))
    along_x = b['size'][0] >= b['size'][2]
    faces = [Face(b, s) for s in ((2, 3) if along_x else (0, 1))]
    for p in parts:
        name = kn(p)
        h, c = p['h'], p['c']
        fc = faces[0] if sk._dot(sk._sub(c, b['pos']), faces[0].N) > 0 else faces[1]
        u, v, hu, hv, d = span(fc, p)
        if name in GOODS and hv > .08:
            if T.i > 0:
                g.face((fc.P(u - hu * .85, v - hv, d - .03), fc.P(u + hu * .85, v - hv, d - .03), fc.P(u + hu * .85, v + hv, d - .03), fc.P(u - hu * .85, v + hv, d - .03)), pfin(p), fc.N)
                continue
            base = fc.P(u, v - hv, d - .075)
            r = min(hu, .075)
            f = pfin(p)
            if name in ('ketchup', 'mustard'):
                lathe(g, [(r * .92, 0), (r, .02), (r, 2 * hv * .62), (r * .5, 2 * hv * .86), (r * .42, 2 * hv)], base, f, 5, scale=(hu / r, 1))
            elif name == 'bun':
                ellipsoid(g, fc.P(u, v - hv + hv * .9, d - .075), hu, hv * .9, .07, f, 6, 3)
            else:
                lathe(g, [(r, 0), (r, 2 * hv * .82), (r * .9, 2 * hv * .86)], base, f, 5, scale=(hu / r, 1))
                lathe(g, [(r * .94, 2 * hv * .86), (r * .94, 2 * hv), (0, 2 * hv)], base, mat('jar-lid', 0xE8D9A8, smooth=.6, metal=.7), 5, scale=(hu / r, 1))
            continue
        if name == 'white' and hv < .05 and hu < .1:
            if T.i == 0:
                lathe(g, [(hu * .8, 0), (hu * .55, 2 * hv), (0, 2 * hv + .01)], fc.P(u, v - hv, d - .045 - .03), pfin(p), 5)
            continue
        kit_part(g, p, T, b)


# ---------------------------------------------------------------------------------------------------------------------------
# The Big Dog's plinth, the bun presses' frames, the sauce bottles
# ---------------------------------------------------------------------------------------------------------------------------
def ring(g, c, w, d, r, y, rad, f, sides=5, n=3):
    sk.tube(g, [(c[0] + x, y, c[1] + z) for x, z in rrect(w, d, r, n)], rad, f, sides, closed=True)


def plinth(g, b, parts, T):
    x0, x1, y0, y1, z0, z1 = ext(b)
    c = (b['pos'][0], b['pos'][2])
    w, d = b['size'][0], b['size'][2]
    n = (4, 2, 1)[T.i]
    loft(g, c, [(0, w + .36, d + .36, .3), (.14, w + .36, d + .36, .3), (.2, w + .2, d + .2, .26), (.3, w + .2, d + .2, .26), (.34, w, d, .2)], kmat('yellow'), n, cap1=False)
    loft(g, c, [(.34, w, d, .2), (2.0, w, d, .2), (3.9, w, d, .2), (5.56, w, d, .2)], mat('plinth', 0xF6E7C8, smooth=.5, dirt=.3), n, cap1=False)
    loft(g, c, [(5.56, w, d, .2), (5.6, w + .1, d + .1, .24), (5.74, w + .16, d + .16, .26), (5.78, w + .1, d + .1, .24)], kmat('yellow'), n, cap1=False)
    loft(g, c, [(5.78, w + .1, d + .1, .24), (5.84, w + .3, d + .3, .3), (5.97, w + .34, d + .34, .32), (6.0, w + .28, d + .28, .3)], kmat('red'), n, cap1=True)
    ring(g, c, w + .02, d + .02, .2, .42, .035, kmat('chrome'), T.small, n)
    bulb = mat('marquee-bulb', 0xFFF2C0, glow=2.6)
    for p in parts:
        name = kn(p)
        if p['f'] == 'plinth':
            if name in ('yellow', 'chrome') and (p['h'][1] > .05 or p['c'][1] < .5 or p['c'][1] > 5.5):
                continue
            if name == 'red' and p['c'][1] > 5.5:
                continue
            if name == 'red':
                kit_part(g, p, T, b)
                for s in range(4):
                    fc = Face(b, s)
                    u, v, hu, hv, dd = span(fc, p)
                    if abs(dd - .012) > .01 or sk._dot(sk._sub(p['c'], b['pos']), fc.N) < 0:
                        continue
                    fc.frame(g, u - hu, u + hu, v - hv, v + hv, dd, .03, kmat('chrome'), T.small, .08, 2)
                    if T.i < 2:
                        k = 9
                        for i in range(k):
                            for vv in (v - hv - .11, v + hv + .11):
                                ellipsoid(g, fc.P(u - hu + 2 * hu * i / (k - 1), vv, dd + .03), .05, .05, .05, bulb, 5, 2)
                continue
        kit_part(g, p, T, b)


def press_post(g, b, parts, T):
    x0, x1, y0, y1, z0, z1 = ext(b)
    steel = mat('press-steel', 0xE2E8EE, smooth=.66, metal=.35, dirt=.2)
    box(g, x0, x1, y0, y1, z0, z1, steel, T, .05, cell=0, skip=('-y',))
    box(g, x0 - .1, x1 + .1, 0, .07, z0 - .1, z1 + .1, kmat('dark'), T, .02, cell=0, skip=('-y',))
    if T.i == 0:
        for sx in (x0 - .05, x1 + .05):
            for sz in (z0 - .05, z1 + .05):
                lathe(g, [(.035, .07), (.035, .1), (0, .11)], (sx, 0, sz), kmat('chrome'), 6)
    for p in parts:
        kit_part(g, p, T, b)


def press_head(g, b, parts, T, hazard):
    x0, x1, y0, y1, z0, z1 = ext(b)
    box(g, x0, x1, y0, y1, z0, z1, mat('press-head', 0xE0302A, smooth=.66, dirt=.25), T, .1, cell=(1.2, 3, 6)[T.i])
    chrome, red = kmat('chrome'), kmat('red')
    if hazard is not None:
        a = hazard['a']
        head = a[1] + hazard['size'][1] / 2 + 1.5
        # the ram's sleeve under the head, the bell's yoke on it
        lathe(g, [(.3, head - 1.2), (.36, head - 1.17), (.36, head - 1.05), (.33, head - 1.02), (.33, head - .1), (.42, head - .06), (.42, head)], (a[0], 0, a[2]), red, T.round, cap0=True)
        lathe(g, [(.26, head - 1.24), (.3, head - 1.2)], (a[0], 0, a[2]), chrome, T.round, cap0=True)
        bz, by = a[2] + 1.2, head + 1.3
        for sx in (-.5, .5):
            sk.cylinder(g, (a[0] + sx, y1, bz), (a[0] + sx, by + .36, bz), .035, chrome, T.small, caps=False)
        sk.cylinder(g, (a[0] - .54, by + .36, bz), (a[0] + .54, by + .36, bz), .035, chrome, T.small)
    for p in parts:
        name = kn(p)
        if p['f'] == 'press-head' and p['c'][1] > y1 and name == 'chrome':
            lathe(g, [(.17, 0), (.17, .05), (.1, .08)], (p['c'][0], y1, p['c'][2]), chrome, T.small + 2)
            continue
        if p['f'] == 'press-head' and p['c'][1] > y1 and name == 'red':
            ellipsoid(g, (p['c'][0], p['c'][1], p['c'][2]), .07, .09, .07, mat('beacon', 0xFF3A2A, glow=1.6), 6, 3)
            continue
        kit_part(g, p, T, b)


def tower(g, b, parts, T):
    x0, x1, y0, y1, z0, z1 = ext(b)
    c = (b['pos'][0], b['pos'][2])
    w = b['size'][0]
    ketchup = c[0] < 0
    col = 0xE3241C if ketchup else 0xFFC81E
    sauce = mat('bottle-%06x' % col, col, smooth=.78, dirt=.15)
    n = (4, 2, 2)[T.i]
    r = .3
    loft(g, c, [(0, w - .06, w - .06, r), (.05, w, w, r), (4.7, w, w, r), (5.2, w - .12, w - .12, r + .1), (5.7, w - .6, w - .6, .5), (6.0, 1.56, 1.56, .6)], sauce, n, cap1=True)
    loft(g, c, [(0, w + .03, w + .03, r), (.35, w + .03, w + .03, r)], kmat('dark'), n, cap1=False)
    label = mat('label', 0xFFFFFF, smooth=.5, dirt=.25)
    loft(g, c, [(1.92, w + .02, w + .02, r), (2.0, w + .02, w + .02, r)], kmat('dark'), n, cap1=False)
    loft(g, c, [(2.0, w + .024, w + .024, r), (3.5, w + .024, w + .024, r)], label, n, cap1=False)
    loft(g, c, [(3.5, w + .02, w + .02, r), (3.58, w + .02, w + .02, r)], kmat('dark'), n, cap1=False)
    # the cap: a ribbed white twist cap and its nozzle, a bead of sauce at the tip
    white = mat('cap-white', 0xFFFFFF, smooth=.62, dirt=.2)
    lathe(g, [(.72, 6.0), (.8, 6.04), (.8, 6.3), (.74, 6.36), (.74, 6.92), (.68, 7.0), (.36, 7.04), (.31, 7.48), (.17, 7.54), (.13, 7.93), (.1, 7.97)], (c[0], 0, c[1]), white, T.round + 2)
    if T.i == 0:
        for k in range(16):
            a = 2 * math.pi * k / 16
            part_round(g, (c[0] + .74 * math.cos(a), 6.4, c[1] + .74 * math.sin(a)), (c[0] + .74 * math.cos(a), 6.9, c[1] + .74 * math.sin(a)), .035, white, (-math.sin(a), 0, math.cos(a)), (math.cos(a), 0, math.sin(a)), 0, 180, 2)
    lathe(g, [(.1, 7.97), (.09, 8.03), (0, 8.08)], (c[0], 0, c[1]), sauce, T.small + 2)
    for p in parts:
        name = kn(p)
        if p['f'] == 'tower':
            if p['c'][1] > y1 or name in ('white', 'dark'):
                continue
            if name in ('ketchup', 'mustard'):
                # a drip down the shoulder
                if T.i < 2:
                    for s in range(4):
                        fc = Face(b, s)
                        if sk._dot(sk._sub(p['c'], b['pos']), fc.N) > 0 and abs(span(fc, p)[4]) < .08:
                            q = dict(p)
                            q['c'] = [p['c'][0], min(p['c'][1], 4.7 - p['h'][1]) if p['h'][1] > .1 else p['c'][1], p['c'][2]]
                            relief(g, q, fc.N, T, mat('drip-%06x' % col, ek.shade(col, .82), smooth=.85, dirt=.1), .8)
                continue
        kit_part(g, p, T, b)


# ---------------------------------------------------------------------------------------------------------------------------
# The whole static scenery for one tier
# ---------------------------------------------------------------------------------------------------------------------------
def static_scene(L, T):
    import emporium_fit as fit
    import emporium_props as props
    g = Geo()
    by_owner, by_fam = {}, {}
    for p in L['parts']:
        by_owner.setdefault(p.get('o'), []).append(p)
        by_fam.setdefault(p['f'].split(':')[0], []).append(p)
    floor(g, L, T, by_owner.get('emporium-floor', []))
    roof(g, T)
    pendants(g, by_fam.get('pendant', []), T)
    for p in by_fam.get('conveyor', []) + by_fam.get('door-sign', []):
        kit_part(g, p, T, None)
    presses = {h['id']: h for h in L['hazards'] if h['kind'] == 'press'}
    steps = {}
    for b in L['blocks']:
        i = b['id']
        parts = by_owner.get(i, [])
        if b['role'] != 'static' or i == 'emporium-floor':
            continue
        if i.startswith('emporium-wall-'):
            wall(g, b, parts, T)
        elif i.startswith('emporium-oven-'):
            oven(g, b, parts, T)
        elif '-kwall-' in i:
            kwall(g, b, parts, T)
        elif '-fryer-' in i:
            fryer(g, b, parts, T)
        elif '-shelf-' in i or '-baffle-' in i:
            shelf(g, b, parts, T)
        elif i == 'emporium-plinth':
            plinth(g, b, parts, T)
        elif '-press-' in i and '-post-' in i:
            press_post(g, b, parts, T)
        elif '-press-' in i:
            press_head(g, b, parts, T, presses.get(i.replace('-head', '')))
        elif '-tower-' in i:
            tower(g, b, parts, T)
        elif ('-flight-' in i and i.startswith('emporium-bridge-')) or i.startswith('emporium-stair-'):
            steps.setdefault(i[:-3], []).append(b)
        else:
            fit.block(g, b, parts, T, L)
    # a striped awning over each kitchen's door
    for b in L['blocks']:
        if '-kwall-' in b['id'] and b['pos'][2] > 0:
            x0, x1, y0, y1, z0, z1 = ext(b)
            fa = Face((x0, x1, 0, 4, -1.75, 1.75), 0 if b['pos'][0] < 0 else 1)
            awning(g, fa, -1.75, 1.75, 3.02, .34, .7, T, stripe=.35)
    for stem, blocks in steps.items():
        fit.flight(g, blocks, T, bun=stem.startswith('emporium-bridge-'))
    props.fittings(g, L, T)
    return g


def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument('--layout', required=True)
    ap.add_argument('--out', help='Assets/LocalAssets to write the bank into')
    ap.add_argument('--preview', help='render views of the baked Fine tier')
    ap.add_argument('--tiers', default='0,1,2')
    ap.add_argument('--no-ao', action='store_true')
    ap.add_argument('--static-only', action='store_true')
    ap.add_argument('--views', default='')
    args = ap.parse_args(argv)
    L = sk.load_layout(args.layout)
    ek.setup(L)
    BL.update({b['id']: b for b in L['blocks']})
    RIG.update(L['rig'])
    import emporium_fit as fit
    import emporium_props as props
    fit.BL, fit.RIG = BL, RIG
    props.BL, props.RIG = BL, RIG
    sb = sk.StageBank()
    chunks = sk.Chunks(12.0, (-36.0, -24.0), (-1.0, 9.0))
    floors = (0.0, 3.6)
    t0 = time.time()
    for ti in [int(x) for x in args.tiers.split(',')]:
        T = sk.Tier(ti)
        t = time.time()
        g = static_scene(L, T)
        blocks = props.block_scene(L, T)
        print('tier %s: static %d faces, %.1f s' % (T.name, g.faces, time.time() - t))
        ao = None
        if not args.no_ao:
            ao = sk.bake_ao([g], distance=(1.1, 1.2, 1.4)[ti], samples=(32, 16, 12)[ti], occluders=[blocks])[0]
            ao = .25 + .75 * ao
        bk = sk.Bake(g, ao, floors=floors, seed=11)
        ids = chunks.ids(g)
        for cid in np.unique(ids):
            sb.add('static/%d/%d' % (cid, ti), 'static', sk.to_mesh('static/%d/%d' % (cid, ti), g, bk, ids == cid))
        if args.preview and ti == 0:
            views = [('hall-west', (-17.0, 1.8, -7.6), (4, 4.5, 3), 75), ('hall-east', (16.5, 5.4, 19.8), (-6, 2.5, -4), 75),
                     ('centre', (-5.0, 1.8, -7.0), (1.5, 6.5, 0.5), 72), ('diner-n', (-9.0, 1.8, 13.0), (3, 2.2, 21.5), 70),
                     ('counter', (9.5, 1.7, 11.2), (4.0, .9, 15.2), 60), ('oven', (-12.5, 1.9, 5.0), (-19.5, 2.2, 10), 62),
                     ('press', (-11.5, 1.7, -4.2), (-6.5, 3.6, 0), 68), ('tower', (-6.5, 1.7, 9.5), (-9.9, 4.2, 5.7), 62),
                     ('kitchen-a', (-28.3, 1.8, -1.0), (-19, 2.0, 1.2), 75), ('balcony', (-12, 5.3, 20.6), (6, 4.4, 20.0), 72),
                     ('bridge', (-7.5, 3.4, 4.6), (-3.2, 2.0, 10.5), 62), ('roof', (-12, 1.8, 8), (4, 19, -2), 78),
                     ('booth', (-9.6, 1.5, 18.7), (-12.4, .7, 21.6), 58), ('soda', (12.4, 1.7, 9.8), (15.7, 1.4, 13.6), 55),
                     ('jukebox', (-10.3, 1.6, 10.4), (-12.5, 1.3, 13.6), 55), ('pump', (-8.2, 1.7, 14.2), (-11.3, 1.9, 17.4), 55),
                     ('top', (0, 44, -1), (0, 0, 0), 62), ('griddle', (-10.2, 2.4, -3.6), (-13.6, 0, 0), 60),
                     ('mascot', (-2.5, 5.5, -9.5), (0, 8.2, 0), 50), ('sign', (-9, 9, -12), (0, 14.2, 0), 60),
                     ('lane', (-16.5, 1.2, 12.9), (2, .8, 9.8), 65), ('fridge', (-21.5, 1.7, 18.5), (-26.5, 1.4, 15.4), 60),
                     ('flight-side', (-8.2, 1.5, 4.6), (-3.4, 1.1, 6.2), 62), ('flight-foot', (-3.4, 1.7, .2), (-3.4, 1.3, 6.0), 60),
                     ('stair-hall', (-19.6, 1.7, 16.2), (-18.0, 1.7, 22.0), 68), ('stair-foot', (-25.5, 1.7, 20.6), (-17.0, 2.2, 22.2), 62),
                     ('stair-top', (-12.6, 5.3, 21.3), (-19.0, 1.6, 22.0), 68)]
            if args.views:
                views = [v for v in views if v[0] in args.views.split(',')]
            pv = [(g, bk), (blocks, sk.Bake(blocks, None))] + props.preview_rig(L, T)
            sk.preview(pv, views, args.preview)
        if not args.static_only:
            props.bank_props(sb, L, T, floors, args.no_ao)
        del g
    if args.out:
        for b in L['blocks']:
            sb.cover(b['id'])
        sb.write(args.out, BANK)
    print('done in %.1f s' % (time.time() - t0))


if __name__ == '__main__':
    main()
