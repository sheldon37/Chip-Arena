"""Skyframe's props (docs/arenas/SKYFRAME.md "Stage art"): the breakable blocks' models and the rig's moving parts, each in its
own frame (Core NativeSkyframeArt poses the rig's from Core NativeSkyframeRig's tagged parts: the frame is that part's centre and
turn).

Rig frames (origin; axes):
  crane-top   the mast's axis at the jib's bottom chord; +z along the jib, +y up (the turntable, cab, jib, counter-jib and its
              weights, the cat head and its pendants)
  trolley     the trolley's middle on the jib's bottom chords; +z along the jib
  cable       a unit rope: radius 1, from y -1 to 1 (scaled by the part's half extents: a hoist rope, a sling)
  hook        the hook block's middle; its spreader beam along x, .4 m below
  load        the bundle of beams' middle; along x (6 x .9 x 1.2 m)
  crown-0..2  a tree's crown: the trunk's top; +y up (the rig leans it)
  windsock    the sock's middle; +z from its ring to its tail (1.5 m)
  rotor       a turbine's hub; the axle along x, the blades in the y-z plane (radius 6.5 m), blade 0 along +y
  airship-0/1 the hull's middle; the nose toward +z; a unit hull (radius 1; scaled by the part's half width)
  bird        the body's middle; the beak toward +z;  wing-l / wing-r: the wing's middle, its span along x (.84 m)
  cloud-0..3  a unit cloud island (-1..1 on every axis; scaled by the part's half extents)
Block frames: the block's centre.
"""
import math

import numpy as np

import stagekit as sk
from stagekit import Geo, box, lathe
import skyframe_kit as kit
from skyframe_kit import F, blk, hashf, beam, ibeam, rod, blob

# the far parts are only ever seen from tens of metres: one model serves every tier
SHARED = ('crane-top', 'trolley', 'rotor', 'airship-0', 'airship-1', 'bird', 'wing-l', 'wing-r', 'cloud-0', 'cloud-1', 'cloud-2', 'cloud-3')
VIEWS = [('rig', (0, 418, -62), (8, 404, 0), 50), ('rig-crane', (26, 412, 30), (0, 404, 12), 55), ('rig-near', (-30, 403, -13), (-30, 401.5, 0), 60), ('rig-air', (40, 410, -30), (40, 402, 0), 60)]


def fins(names):
    return tuple(F[n] for n in names)


def rot_x(g, a):
    c, s = math.cos(a), math.sin(a)
    return g.transformed([1, 0, 0, 0, 0, c, -s, 0, 0, s, c, 0])


# ---------------------------------------------------------------------------------------------------------------------------
# What stands still beside the rig: the windsocks' poles, outside the cloud terraces' edges
# ---------------------------------------------------------------------------------------------------------------------------
def fixed(g, L, T):
    S = L['skyframe']
    for (x, y, z, yaw) in S['socks']:
        s = 1 if z > 0 else -1
        sk.cylinder(g, (x, y - S['sockPole'] - 1.5, z), (x, y + .12, z), .035, F['stainless'], (8, 6, 4)[T.i])
        beam(g, (x, y - S['sockPole'] - 1.4, z - s * .2), (x, y - S['sockPole'] - 1.4, z), .06, .06, F['steel'])
        if T.i < 2:
            lathe(g, [(.17, -.02), (.17, .02)], (x, y, z), F['stainless'], 10, axis=(math.sin(yaw), 0, math.cos(yaw)))


# ---------------------------------------------------------------------------------------------------------------------------
# The breakable blocks
# ---------------------------------------------------------------------------------------------------------------------------
def crate(T, sx, sy, sz, seed):
    """A slatted timber crate of plants on its way to a planter."""
    g = Geo()
    hx, hy, hz = sx / 2, sy / 2, sz / 2
    if T.i == 2:
        box(g, -hx, hx, -hy, hy - .12, -hz, hz, F['timber'], None, 0, cell=0)
        box(g, -hx + .1, hx - .1, hy - .12, hy, -hz + .1, hz - .1, F['leaf'], None, 0, cell=0, skip=('-y',))
        return g
    p = .07
    for ax in (-1, 1):
        for az in (-1, 1):
            box(g, ax * hx - (p if ax > 0 else 0), ax * hx + (p if ax < 0 else 0), -hy, hy - .1, az * hz - (p if az > 0 else 0), az * hz + (p if az < 0 else 0), F['timber-dark'], T, .008, cell=0)
    n = 5
    for k in range(n):
        y0 = -hy + .04 + k * (sy - .18) / n
        y1 = y0 + (sy - .18) / n - .05
        f = F['timber' if (k + seed) % 3 else 'timber-dark']
        box(g, -hx + .01, hx - .01, y0, y1, -hz + .015, -hz + .04, f, None, 0, cell=0)
        box(g, -hx + .01, hx - .01, y0, y1, hz - .04, hz - .015, f, None, 0, cell=0)
        box(g, -hx + .015, -hx + .04, y0, y1, -hz + .01, hz - .01, f, None, 0, cell=0)
        box(g, hx - .04, hx - .015, y0, y1, -hz + .01, hz - .01, f, None, 0, cell=0)
    box(g, -hx + .04, hx - .04, -hy + .02, hy - .3, -hz + .04, hz - .04, F['soil'], None, 0, cell=0, skip=('-y',))
    for k in range(9 if T.fine else 5):
        x = (hashf(seed, k) - .5) * (sx - .5)
        z = (hashf(seed, k, 1) - .5) * (sz - .5)
        r = .16 + .1 * hashf(seed, k, 2)
        cols = ('flower-orange', 'leaf', 'leaf-dark') if k % 4 == 0 else ('leaf-light', 'leaf', 'leaf-dark')
        blob(g, (x, hy - .3 + r * .5, z), r, T, seed + k, fins(cols), squash=(1, .9, 1), cut=.5, sides=(8, 6, 5)[T.i])
    return g


def pallet(T, sx, sy, sz, seed):
    """A pallet of paving slabs, strapped."""
    g = Geo()
    hx, hy, hz = sx / 2, sy / 2, sz / 2
    if T.i == 2:
        box(g, -hx, hx, -hy, -hy + .14, -hz, hz, F['timber'], None, 0, cell=0)
        box(g, -hx + .03, hx - .03, -hy + .14, hy, -hz + .03, hz - .03, F['paving'], None, 0, cell=0, skip=('-y',))
        return g
    for z in (-hz + .06, 0, hz - .06):
        box(g, -hx, hx, -hy, -hy + .09, z - .05, z + .05, F['timber-dark'], None, 0, cell=0)
    for k in range(5):
        x = -hx + (k + .5) * sx / 5
        box(g, x - sx / 10 + .012, x + sx / 10 - .012, -hy + .09, -hy + .12, -hz, hz, F['timber'], None, 0, cell=0)
    layers = 6
    lh = (sy - .14) / layers
    for l in range(layers):
        y0 = -hy + .13 + l * lh
        for c in range(2):
            x0 = -hx + .03 + c * (sx - .06) / 2
            off = (hashf(seed, l, c) - .5) * .02
            box(g, x0 + .004 + off, x0 + (sx - .06) / 2 - .004 + off, y0 + .004, y0 + lh - .004, -hz + .03, hz - .03, F['paving' if (l + c) % 3 else 'paving-dark'], T, .006, cell=0)
    for x in (-hx * .45, hx * .45):
        box(g, x - .025, x + .025, -hy + .1, hy + .006, -hz + .02, hz - .02, F['crane-dark'], None, 0, cell=0, skip=('-y',))
    return g


def block_items(L, T):
    """[(block id, key, Geo)]: blocks with one key share one model."""
    out = []
    for b in L['blocks']:
        if b['role'] == 'static':
            continue
        sx, sy, sz = b['size']
        kind = 'crate' if '-crate-' in b['id'] else 'pallet'
        out.append((b['id'], '%s-%.2f-%.2f-%.2f' % (kind, sx, sy, sz), kind, (sx, sy, sz)))
    return out


def occluders(L, T):
    """The breakable blocks where they stand, for the stage's occlusion bake."""
    out = []
    for i, key, kind, (sx, sy, sz) in block_items(L, T):
        p = L['byid'][i]['pos']
        g = Geo()
        box(g, -sx / 2, sx / 2, -sy / 2, sy / 2, -sz / 2, sz / 2, F['timber'], None, 0, cell=0)
        out.append(g.transformed([1, 0, 0, p[0], 0, 1, 0, p[1], 0, 0, 1, p[2]]))
    return out


# ---------------------------------------------------------------------------------------------------------------------------
# The tower crane
# ---------------------------------------------------------------------------------------------------------------------------
def crane_top(L, T):
    g = Geo()
    C = L['skyframe']['crane']
    Lj, Cj, dep, w = C['jibLength'], C['counterJib'], C['jibDepth'], C['jibHalf']
    Y, D = F['crane'], F['crane-dark']
    lathe(g, [(1.55, -.3), (1.7, -.14), (1.7, .14), (1.5, .2)], (0, 0, 0), D, 14, cap0=True, cap1=True)
    box(g, -1.25, 1.25, -1.3, -.3, -1.25, 1.25, Y, T, .06, cell=0)
    # the cab, off to the side by the jib's foot: a rounded box with a wrapped windscreen
    cx, cz = 1.95, 1.5
    box(g, cx - .8, cx + .8, -1.65, -.05, cz - .8, cz + .8, Y, T, .1, cell=0)
    g.face(((cx - .7, -1.0, cz + .805), (cx + .7, -1.0, cz + .805), (cx + .7, -.2, cz + .805), (cx - .7, -.2, cz + .805)), F['glass'], (0, 0, 1))
    g.face(((cx - .7, -1.55, cz + .805), (cx + .7, -1.55, cz + .805), (cx + .7, -1.05, cz + .805), (cx - .7, -1.05, cz + .805)), F['glass'], (0, 0, 1))
    g.face(((cx + .805, -1.0, cz - .6), (cx + .805, -1.0, cz + .7), (cx + .805, -.2, cz + .7), (cx + .805, -.2, cz - .6)), F['glass'], (1, 0, 0))
    g.face(((cx - .5, -.045, cz - .5), (cx + .5, -.045, cz - .5), (cx + .5, -.045, cz + .5), (cx - .5, -.045, cz + .5)), F['paint-white'], (0, 1, 0))
    # the jib: two bottom chords, the top chord, laced in tubes
    ty = dep
    def top(z):
        return ty - .5 * (z - 1.2) / (Lj - 2.7)
    for s in (-1, 1):
        beam(g, (s * w, 0, 1.2), (s * w, 0, Lj), .17, .17, Y)
    beam(g, (0, top(1.2), 1.2), (0, top(Lj - 1.5), Lj - 1.5), .17, .17, Y)
    beam(g, (0, top(Lj - 1.5), Lj - 1.5), (0, .1, Lj), .12, .12, Y)
    z = 1.2
    k = 0
    while z < Lj - 2:
        z1 = min(Lj - 1.5, z + 2.4)
        for s in (-1, 1):
            rod(g, (s * w, 0, z), (0, top(z1), z1), .04, Y, 4)
            rod(g, (0, top(z), z), (s * w, 0, z), .04, Y, 4)
        rod(g, (-w, 0, z), (w, 0, z), .035, Y, 4)
        rod(g, (-w if k % 2 else w, 0, z), (w if k % 2 else -w, 0, z1), .03, Y, 4)
        z = z1
        k += 1
    box(g, -w - .1, w + .1, -.12, .3, Lj - .08, Lj + .08, D, None, 0, cell=0)
    # the counter-jib: its deck, the winch, the weights hung under its end
    for s in (-1, 1):
        beam(g, (s * w, 0, -1.2), (s * w, 0, -Cj), .17, .17, Y)
        rod(g, (s * (w + .05), 1.0, -1.4), (s * (w + .05), 1.0, -Cj + .2), .025, Y, 4)
        for kk in range(5):
            zz = -1.6 - kk * (Cj - 2) / 4
            rod(g, (s * (w + .05), .08, zz), (s * (w + .05), 1.0, zz), .025, Y, 4)
    box(g, -w, w, .06, .1, -Cj, -1.2, F['deck'], None, 0, cell=0)
    box(g, -.75, .75, .1, 1.1, -5.6, -3.2, D, T, .05, cell=0)
    sk.cylinder(g, (-.7, .75, -2.4), (.7, .75, -2.4), .42, F['steel'], 10)
    for kk in range(4):
        zz = -Cj + 1 + kk * 1.05
        box(g, -w - .18, w + .18, -1.55, -.08, zz - .42, zz + .42, F['weight'], T, .04, cell=0)
    # the cat head and its pendants, the beacons
    cat = 8.0
    for s in (-1, 1):
        beam(g, (s * 1.0, .1, .4), (0, cat, 0), .16, .16, Y)
        beam(g, (s * 1.0, .1, -1.0), (0, cat, 0), .14, .14, Y)
    rod(g, (-.9, 3.4, .2), (.9, 3.4, .2), .04, Y, 4)
    for s in (-.12, .12):
        rod(g, (s, cat, 0), (s, top(Lj * .62) + .05, Lj * .62), .035, F['cable'], 4)
        rod(g, (s, cat, 0), (s, .25, -Cj + .6), .035, F['cable'], 4)
    blob(g, (0, cat + .25, 0), .14, T, 3, fins(('beacon', 'beacon', 'beacon')), lumps=0, sides=6)
    blob(g, (0, .3, Lj), .13, T, 4, fins(('beacon', 'beacon', 'beacon')), lumps=0, sides=6)
    blob(g, (0, .3, -Cj), .11, T, 5, fins(('beacon', 'beacon', 'beacon')), lumps=0, sides=6)
    return g


def trolley(L, T):
    g = Geo()
    w = L['skyframe']['crane']['jibHalf']
    box(g, -w - .12, w + .12, -.2, .06, -.75, .75, F['crane-dark'], T, .03, cell=0)
    for s in (-1, 1):
        for z in (-.5, .5):
            sk.cylinder(g, (s * w - .09, .22, z), (s * w + .09, .22, z), .14, F['steel'], 8)
        sk.cylinder(g, (s * .28 - .05, -.3, 0), (s * .28 + .05, -.3, 0), .2, F['crane'], 8)
    return g


def cable(T):
    g = Geo()
    sk.cylinder(g, (0, -1, 0), (0, 1, 0), 1.0, F['cable'], (6, 5, 4)[T.i], caps=False)
    return g


def hook(T):
    g = Geo()
    Y, D = F['crane'], F['crane-dark']
    box(g, -.32, .32, -.4, .45, -.2, .2, Y, T, .06, cell=0)
    if T.i < 2:
        for s in (-1, 1):
            sk.cylinder(g, (s * .28, .12, -.22), (s * .28, .12, .22), .16, D, (10, 8, 6)[T.i])
        sk.cylinder(g, (0, -.52, 0), (0, -.36, 0), .07, F['steel'], 6)
    # the spreader beam under it, striped, and its shackles
    n = 8 if T.i < 2 else 2
    for k in range(n):
        x0, x1 = -2.3 + 4.6 * k / n, -2.3 + 4.6 * (k + 1) / n
        box(g, x0, x1, -.49, -.31, -.09, .09, Y if k % 2 == 0 else D, None, 0, cell=0, skip=tuple(s for s, c in (('-x', k > 0), ('+x', k < n - 1)) if c))
    for s in (-1, 1):
        box(g, s * 2.2 - .05, s * 2.2 + .05, -.6, -.47, -.05, .05, F['steel'], None, 0, cell=0)
    return g


def load(L, T):
    import skyframe_art as art
    g = Geo()
    h = [x for x in L['hazards'] if x['kind'] == 'crane'][0]
    sx, sy, sz = h['size']
    art.bundle(g, 0, -sy / 2, 0, sx, sy, sz, T, rows=2, cols=3)
    return g


# ---------------------------------------------------------------------------------------------------------------------------
# Trees, windsocks, rotors
# ---------------------------------------------------------------------------------------------------------------------------
def crown(T, v):
    """A tree's crown from the trunk's top: boughs and masses of leaves, lit gold where they face the low sun."""
    g = Geo()
    n = (15, 10, 6)[T.i]
    seed = 40 + v * 17
    for k in range(4 if T.i < 2 else 0):
        a = k * math.pi / 2 + v
        rod(g, (0, -.3, 0), (math.cos(a) * (.9 + .3 * hashf(seed, k)), .9 + .5 * hashf(seed, k, 1), math.sin(a) * (.9 + .3 * hashf(seed, k, 2))), .07, F['bark'], (6, 5, 4)[T.i])
    rod(g, (0, -.4, 0), (0, 1.3, 0), .11, F['bark'], (6, 5, 4)[T.i])
    for k in range(n):
        a = k * 2.399 + v
        rr = (.4 + 1.25 * math.sqrt((k + .5) / n)) * (1.0 + .12 * v)
        y = .9 + 2.0 * hashf(seed, k, 3) * (1 - rr / 2.6) + (.9 if k == 0 else 0)
        r = (1.05 - .25 * hashf(seed, k, 4)) * (1.25 if k == 0 else 1.0)
        cols = ('leaf-gold', 'leaf-light', 'leaf-dark') if hashf(seed, k, 5) > .7 else ('leaf-light', 'leaf', 'leaf-dark')
        blob(g, (math.cos(a) * rr if k else 0, y, math.sin(a) * rr if k else 0), r, T, seed + k, fins(cols), squash=(1, .78, 1), lumps=.3, freq=2.0, sides=(10, 7, 6)[T.i])
    return g


def windsock(L, T):
    g = Geo()
    ln = L['skyframe']['sockLength']
    bands = 5 if T.i < 2 else 2
    for k in range(bands):
        z0, z1 = -ln / 2 + ln * k / bands, -ln / 2 + ln * (k + 1) / bands
        r0, r1 = .16 - .09 * k / bands, .16 - .09 * (k + 1) / bands
        lathe(g, [(r0, z0), (r1, z1)], (0, 0, 0), F['cloth-orange' if k % 2 == 0 else 'paint-white'], (10, 8, 6)[T.i], axis=(0, 0, 1))
    return g


def rotor(L, T):
    g = Geo()
    R = L['skyframe']['turbineRadius']
    W = F['white']
    lathe(g, [(.02, -1.0), (.3, -.7), (.46, -.25), (.5, .2), (.5, .5)], (0, 0, 0), W, 10, axis=(1, 0, 0), cap1=True)
    blade = Geo()
    # a blade along +y: a broad root tapering to the tip, twisted a little, a red tip band
    st = [(0.4, .32, .16, .5), (1.4, .42, .12, .3), (3.4, .3, .08, .15), (R - .7, .16, .04, .05), (R, .08, .02, 0)]
    for i in range(len(st) - 1):
        (y0, c0, t0, a0), (y1, c1, t1, a1) = st[i], st[i + 1]
        def sec(y, c, t, a):
            ca, sa = math.cos(a), math.sin(a)
            pts = [(-t, -c * .5), (t, -c * .2), (t * .4, c * .5), (-t, c * .5)]
            return [(x * ca - z * sa, y, x * sa + z * ca) for x, z in pts]
        A, B = sec(y0, c0, t0, a0), sec(y1, c1, t1, a1)
        f = F['paint-red'] if i == len(st) - 2 else W
        for k in range(4):
            k1 = (k + 1) % 4
            blade.face((A[k], A[k1], B[k1], B[k]), f)
    for k in range(3):
        g.extend(rot_x(blade, k * 2 * math.pi / 3))
    return g


# ---------------------------------------------------------------------------------------------------------------------------
# Airships, birds, clouds
# ---------------------------------------------------------------------------------------------------------------------------
def airship(T, v):
    """A unit airship (hull radius 1, nose toward +z): the hull in gores, its nose cone and tail in the accent, the gondola
    with lit windows, four fins, two engine pods."""
    g = Geo()
    hl = 3.57
    hull, accent = (F['cloth'], F['cloth-orange']) if v == 0 else (F['white'], F['tarp'])
    n = 14
    prof = []
    for i in range(n + 1):
        t = i / n
        z = -hl + 2 * hl * t
        r = math.sin(math.pi * t ** .8) ** .7 if 0 < t < 1 else 0.0
        prof.append((max(r, .02), z))
    sides = 14
    sc = (1.0, 1.0) if v == 0 else (1.15, .82)
    for i in range(n):
        f = accent if (i < 2 or i >= n - 1 or i == n // 2) else hull
        lathe(g, prof[i:i + 2], (0, 0, 0), f, sides, axis=(0, 0, 1), scale=sc)
    # the gondola under the hull
    gy = -sc[1] - .02
    box(g, -.3, .3, gy - .42, gy + .08, -.9, 1.0, F['timber'] if v == 0 else F['steel'], T, .08, cell=0)
    for s in (-1, 1):
        g.face(((s * .305, gy - .2, -.75), (s * .305, gy - .2, .85), (s * .305, gy - .02, .85), (s * .305, gy - .02, -.75)), F['window'], (s, 0, 0))
        # an engine pod on a strut each side, its propeller's disc
        sk.cylinder(g, (s * .3, gy - .2, -.2), (s * .95, gy - .1, -.2), .04, F['steel'], 4)
        lathe(g, [(.03, -.3), (.14, -.1), (.14, .2), (.05, .34)], (s * 1.0, gy - .1, -.2), accent, 8, axis=(0, 0, 1))
        lathe(g, [(.02, 0), (.3, .005)], (s * 1.0, gy - .1, -.52), F['crane-dark'], 8, axis=(0, 0, 1))
    # the fins at the tail
    for k in range(4):
        a = k * math.pi / 2 + (math.pi / 4 if v else 0)
        ca, sa = math.cos(a), math.sin(a)
        def P(r, z):
            return (ca * r * sc[0], sa * r * sc[1], z)
        pts = (P(.45, -1.9), P(1.25, -2.9), P(1.25, -3.5), P(.3, -3.3))
        nn = (-sa, ca, 0)
        g.face(pts, accent, nn)
        g.face(pts, accent, (sa, -ca, 0))
    if v == 1:
        # a dorsal sail
        pts = ((0, sc[1] - .05, 1.2), (0, sc[1] + .9, -.2), (0, sc[1] + .7, -1.6), (0, sc[1] - .1, -1.8))
        g.face(pts, accent, (1, 0, 0))
        g.face(pts, accent, (-1, 0, 0))
    return g


def bird():
    g = Geo()
    lathe(g, [(.005, -.2), (.045, -.1), (.07, .04), (.05, .16), (.01, .26)], (0, 0, 0), F['bird'], 5, axis=(0, 0, 1))
    g.face(((-.05, 0, -.18), (.05, 0, -.18), (.09, 0, -.36), (-.09, 0, -.36)), F['bird'], (0, 1, 0))
    g.face(((-.05, 0, -.18), (.05, 0, -.18), (.09, 0, -.36), (-.09, 0, -.36)), F['bird'], (0, -1, 0))
    return g


def wing(side):
    g = Geo()
    s = side
    pts = ((-s * .42, 0, -.02), (-s * .42, 0, .1), (s * .1, 0, .16), (s * .42, 0, .02), (s * .2, 0, -.1))
    g.face(pts, F['bird'], (0, 1, 0))
    g.face(pts, F['bird-light'], (0, -1, 0))
    return g


def cloud(v):
    """A unit cloud island: a heap of soft lumps on a flat base, fitted to -1..1."""
    g = Geo()
    T = sk.Tier(1)
    seed = 70 + v * 31
    n = 6 + v % 3
    for k in range(n):
        a = k * 2.4 + v
        d = 0 if k == 0 else (.35 + .55 * hashf(seed, k))
        r = (.62 if k == 0 else .3 + .25 * hashf(seed, k, 1))
        y = (.25 if k == 0 else .05 + .3 * hashf(seed, k, 2))
        blob(g, (math.cos(a) * d * 1.3, y, math.sin(a) * d * .9), r, T, seed + k, fins(('cloud-top', 'cloud-mid', 'cloud-low')), squash=(1.5, .62, 1.15), lumps=.28, freq=1.3, cut=.55, sides=10)
    P = np.frombuffer(g.p, dtype=np.float32).reshape(-1, 3)
    lo, hi = P.min(axis=0), P.max(axis=0)
    c, h = (lo + hi) / 2, (hi - lo) / 2
    return g.transformed([1 / h[0], 0, 0, -c[0] / h[0], 0, 1 / h[1], 0, -c[1] / h[1], 0, 0, 1 / h[2], -c[2] / h[2]])


def items(L, T):
    """[(slot name, Geo, rest (where it stands in the stage, for the bake's patterns), place (x, y, z, scale) in the preview)]"""
    jy = L['skyframe']['crane']['jibY']
    out = [('crane-top', crane_top(L, T), (0, jy, 0), (0, 4, 0, .5)), ('trolley', trolley(L, T), (0, jy, 15), (0, 3.6, 11, .5)), ('cable', cable(T), (15, 20, 0), (-24, 2, 0, .2)),
           ('hook', hook(T), (15, 12, 0), (-30, 3.2, 0, 1)), ('load', load(L, T), (15, 11, 0), (-30, 1, 0, 1))]
    for v in range(3):
        out.append(('crown-%d' % v, crown(T, v), (-22 + v * 14, 4.4, 12), (-52 + v * 6, 1, 0, 1)))
    out.append(('windsock', windsock(L, T), (-11.5, .3, 22), (-27, 3, 0, 1)))
    out.append(('rotor', rotor(L, T), (-35, 15, 30), (24, 4, 0, .5)))
    for v in range(2):
        out.append(('airship-%d' % v, airship(T, v), (100, 20, 80), (36 + v * 9, 3, 0, 1)))
    out += [('bird', bird(), (20, 25, 20), (-26, 4, 0, 2)), ('wing-l', wing(-1), (20, 25, 20), (-27, 4, 0, 2)), ('wing-r', wing(1), (20, 25, 20), (-25, 4, 0, 2))]
    for v in range(4):
        out.append(('cloud-%d' % v, cloud(v), (100, -34, 100), (-16 + v * 5, 8, 0, 2)))
    return out


_shared = {}


def bank_props(sb, L, T, no_ao):
    ti = T.i
    its = [it for it in items(L, T) if it[0] not in SHARED or it[0] not in _shared]
    geos = [it[1] for it in its]
    aos = [None] * len(geos)
    if not no_ao:
        apart = [g.transformed([1, 0, 0, 80.0 * i, 0, 1, 0, 0, 0, 0, 1, 0]) for i, g in enumerate(geos)]
        aos = sk.bake_ao(apart, distance=.5, samples=(24, 12, 8)[ti])
    ident = [1.0, 0, 0, 0, 0, 1.0, 0, 0, 0, 0, 1.0, 0]
    for (name, g, rest, place), ao in zip(its, aos):
        if name.startswith('cloud') or name.startswith('airship') or name in ('bird', 'wing-l', 'wing-r'):
            ao = None if ao is None else 1.0 - (1.0 - ao) * .35
        Pw = g.arrays()[0].astype(np.float64) + np.array(rest)
        bk = sk.Bake(g, ao, world=Pw, floors=(), seed=7)
        if name in SHARED:
            om = sk.to_mesh('rig/%s' % name, g, bk)
            mi = sb.mesh(om)
            _shared[name] = mi
            st = sb.stats.setdefault(('rig', 'all'), [0, 0, 0])
            st[0] += 1
            st[1] += sum(len(s) for s in om.submeshes) // 3
            st[2] += len(om.positions)
        else:
            sb.add('rig/%s/%d' % (name, ti), 'rig', sk.to_mesh('rig/%s/%d' % (name, ti), g, bk))
    for name in SHARED:
        sb.bank.prop('rig/%s/%d' % (name, ti), 'rig', [(_shared[name], ident)])
    # the breakable blocks: one model per kind and size
    done = {}
    for i, key, kind, (sx, sy, sz) in block_items(L, T):
        if key not in done:
            g = (crate if kind == 'crate' else pallet)(T, sx, sy, sz, len(done) + 1)
            ao = None if no_ao else sk.bake_ao([g], distance=.4, samples=(24, 12, 8)[ti])[0]
            p = L['byid'][i]['pos']
            bk = sk.Bake(g, ao, world=g.arrays()[0].astype(np.float64) + np.array(p), floors=(p[1] - sy / 2,), seed=9)
            om = sk.to_mesh('block/%s/%d' % (key, ti), g, bk)
            done[key] = sb.mesh(om)
            st = sb.stats.setdefault(('block', str(ti)), [0, 0, 0])
            st[0] += 1
            st[1] += sum(len(s) for s in om.submeshes) // 3
            st[2] += len(om.positions)
        sb.bank.prop('block/%s/%d' % (i, ti), 'block', [(done[key], ident)])
