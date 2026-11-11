"""The Factory's modelling helpers (docs/arenas/STAGE-ART.md, docs/arenas/FACTORY-ART.md), on stagekit: the
finishes (the proposal's palette 3.10 and the kit kinds' colours), steel sections (I-beams, tubes, plates with bolts), corrugated
sheet, hazard stripes clipped to a rectangle, a block's canonical half turn (the map is point-symmetric: every south / B piece is
its north / A twin turned half round), and the fallback that rebuilds any kit part as a finished piece (bars round, pipes and
flanges turned, decals flat, stripes painted, lamps glowing). Used by factory_art.py (the building), factory_plant.py (machines,
stock, docks) and factory_rig.py (the rigs and the breakable blocks). Everything is in the game's space (Unity: metres, +y up)."""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stagekit as sk  # noqa: E402
from stagekit import Geo, box, obox  # noqa: E402

KINDS = []   # kit kind index -> short name ("rail", "stripe", ...)
KCOL = []    # kit kind index -> the look's colour
A = sk
Y = (0.0, 1.0, 0.0)

# name -> (hex, detail, smooth, metal, dirt, wear, param, glow)
MATS = {
    'floor': (0x6E6A63, A.DETAIL_PLASTER, .42, 0, .8, 0, 0, 0), 'plinth': (0x8C877D, A.DETAIL_PLASTER, .25, 0, .8, .1, 0, 0),
    'baywall': (0x9A9488, A.DETAIL_PLASTER, .25, 0, .8, .12, 0, 0), 'block': (0xA49D90, A.DETAIL_PLASTER, .2, 0, .8, .1, 0, 0),
    'concrete': (0x7E7A72, A.DETAIL_PLASTER, .25, 0, .8, .1, 0, 0), 'pit': (0x141414, A.DETAIL_NONE, .2, 0, .2, 0, 0, 0),
    'clad': (0x5E6E78, A.DETAIL_NONE, .45, .4, .7, .12, 0, 0), 'roof': (0x5A646C, A.DETAIL_NONE, .4, .4, .7, .1, 0, 0),
    'truss': (0x46525C, A.DETAIL_NONE, .4, .35, .6, .25, 0, 0), 'steel': (0x3C4E5C, A.DETAIL_NONE, .42, .35, .6, .3, 0, 0),
    'runway': (0x4A5864, A.DETAIL_NONE, .42, .35, .6, .3, 0, 0), 'frame': (0x2C343A, A.DETAIL_NONE, .4, .3, .5, .25, 0, 0),
    'yellow': (0xE8B21A, A.DETAIL_NONE, .5, .1, .45, .3, 0, 0), 'crane': (0xE3A21A, A.DETAIL_NONE, .5, .15, .5, .3, 0, 0),
    'press': (0xC79A22, A.DETAIL_NONE, .5, .15, .5, .3, 0, 0), 'hazard-y': (0xF4B321, A.DETAIL_NONE, .45, 0, .45, .2, 0, 0),
    'hazard-k': (0x1C1A18, A.DETAIL_NONE, .4, 0, .3, .2, 0, 0), 'danger': (0xB8322A, A.DETAIL_NONE, .5, 0, .4, .2, 0, 0),
    'green': (0x4F7A6A, A.DETAIL_NONE, .5, .15, .55, .3, 0, 0), 'green2': (0x557F70, A.DETAIL_NONE, .5, .15, .55, .3, 0, 0),
    'cabinet': (0x8E9AA0, A.DETAIL_NONE, .5, .2, .5, .2, 0, 0), 'rack': (0x2E5584, A.DETAIL_NONE, .48, .15, .5, .3, 0, 0),
    'bright': (0x9A9692, A.DETAIL_NONE, .62, .5, .4, .15, 0, 0), 'billet': (0x7A6E64, A.DETAIL_NONE, .4, .45, .5, .15, 0, 0),
    'oxide': (0x8A4E32, A.DETAIL_NONE, .3, .2, .6, .2, 0, 0), 'ladle': (0x2E2B29, A.DETAIL_IRON, .35, .5, .5, .2, 0, 0),
    'iron': (0x3E3A38, A.DETAIL_NONE, .4, .45, .5, .2, 0, 0), 'chrome': (0xB8BEC4, A.DETAIL_NONE, .82, .5, .15, 0, 0, 0),
    'black': (0x1A1B1D, A.DETAIL_NONE, .35, .1, .3, .15, 0, 0), 'rubber': (0x1E1E20, A.DETAIL_NONE, .2, 0, .4, 0, 0, 0),
    'timber': (0x8A6A44, A.DETAIL_WOOD, .25, 0, .7, .2, 0, 0), 'timber-z': (0x8A6A44, A.DETAIL_WOOD, .25, 0, .7, .2, 2, 0),
    'timber-y': (0x86663F, A.DETAIL_WOOD, .25, 0, .7, .2, 1, 0), 'carton': (0xB49066, A.DETAIL_NONE, .2, 0, .5, 0, 0, 0),
    'bin': (0x3D6E9E, A.DETAIL_NONE, .5, 0, .4, .15, 0, 0), 'grating': (0x7C8388, A.DETAIL_TILE, .45, .45, .5, .2, .05, 0),
    'mesh': (0x6C767C, A.DETAIL_TILE, .4, .4, .5, .1, .09, 0), 'paint-walk': (0xC79A1E, A.DETAIL_PLASTER, .4, 0, .7, 0, 0, 0),
    'paint-white': (0xD8D4CA, A.DETAIL_PLASTER, .4, 0, .7, 0, 0, 0), 'paint-zone': (0x2A2724, A.DETAIL_PLASTER, .4, 0, .5, 0, 0, 0),
    'glass': (0x39505C, A.DETAIL_NONE, .92, .3, .1, 0, 0, 0), 'white': (0xD9DDD8, A.DETAIL_NONE, .5, 0, .5, .1, 0, 0),
    'office': (0xC8C1AE, A.DETAIL_NONE, .4, .2, .6, .1, 0, 0), 'box-rust': (0xA8552C, A.DETAIL_NONE, .4, .3, .7, .15, 0, 0),
    'box-green': (0x3F6B4E, A.DETAIL_NONE, .4, .3, .7, .15, 0, 0), 'fork': (0xE0A11B, A.DETAIL_NONE, .5, .15, .5, .3, 0, 0),
    'drum': (0x2C5C8A, A.DETAIL_NONE, .5, .3, .5, .3, 0, 0), 'drum-red': (0xA83A2C, A.DETAIL_NONE, .5, .3, .5, .3, 0, 0),
    'slag': (0x2A1C16, A.DETAIL_IRON, .2, 0, .2, 0, 0, 0), 'tar': (0x2E3033, A.DETAIL_PLASTER, .2, 0, .6, 0, 0, 0),
    'steam': (0xB4B8BA, A.DETAIL_CLOTH, .25, 0, .6, 0, 0, 0), 'water': (0x3E7A9C, A.DETAIL_NONE, .5, .2, .5, .2, 0, 0),
    'gas': (0xD2A52A, A.DETAIL_NONE, .5, .2, .5, .2, 0, 0), 'motor': (0xC8602A, A.DETAIL_NONE, .5, .2, .5, .25, 0, 0),
    # light
    'melt': (0xFF7A22, A.DETAIL_GLOW, .5, 0, 0, 0, 0, 3.2), 'melt-dim': (0xE0481A, A.DETAIL_GLOW, .5, 0, 0, 0, 0, 1.3),
    'sodium': (0xFFA14A, A.DETAIL_GLOW, .5, 0, 0, 0, 0, 4.0), 'daylight': (0xC4D6E2, A.DETAIL_GLOW, .5, 0, 0, 0, 0, .85),
    'flood': (0xDCE8FF, A.DETAIL_GLOW, .5, 0, 0, 0, 0, 3.5), 'lit-window': (0xFFE2AE, A.DETAIL_GLOW, .5, 0, 0, 0, 0, 1.25),
    'lamp-g': (0x37D95A, A.DETAIL_GLOW, .5, 0, 0, 0, 0, 2.0), 'lamp-a': (0xFFB02E, A.DETAIL_GLOW, .5, 0, 0, 0, 0, 2.2),
    'lamp-r': (0xE0402E, A.DETAIL_GLOW, .5, 0, 0, 0, 0, 2.2), 'screen': (0x3CB878, A.DETAIL_GLOW, .5, 0, 0, 0, 0, 1.2),
    'white-glow': (0xFFFFFF, A.DETAIL_GLOW, .5, 0, 0, 0, 0, 1.0),
}

# kit kind -> the finish its parts take when they are rebuilt as they are
KIND_MAT = {
    'rail': 'yellow', 'toe': 'yellow', 'bracket': 'steel', 'truss': 'truss', 'frame': 'frame', 'pipe-steam': 'steam', 'pipe-water': 'water',
    'pipe-gas': 'gas', 'pipe-air': 'water', 'flange': 'black', 'valve': 'danger', 'void': 'pit', 'plate': 'iron', 'paint-walk': 'paint-walk',
    'paint-zone': 'paint-zone', 'paint-white': 'paint-white', 'blade': 'chrome', 'hub': 'black', 'crane': 'crane', 'crane-dark': 'black',
    'cab-glass': 'glass', 'cable': 'black', 'hook': 'crane', 'load': 'oxide', 'strap': 'black', 'ram': 'press', 'grate': 'grating',
    'bar': 'billet', 'coil': 'bright', 'wheel': 'rubber', 'panel': 'cabinet', 'sign': 'white', 'ladle': 'ladle', 'ladle-rim': 'iron',
    'track': 'iron', 'concrete': 'concrete', 'door': 'clad', 'machine': 'green', 'chrome': 'chrome', 'black': 'black', 'motor': 'motor',
    'timber': 'timber-z', 'danger': 'danger', 'joint': 'black', 'box': 'carton', 'bin': 'bin', 'cladding': 'clad', 'roof': 'roof',
    'red': 'danger', 'green': 'green',
}


def setup(L):
    KINDS[:] = L['kinds']
    KCOL[:] = L['kindColors']


def mat(name):
    """A finish by its name in MATS (made once)."""
    h, d, s, m, dirt, wear, param, glow = MATS[name]
    return sk.fin('f-' + name, h, d, s, m, dirt, 0.0, glow, param, wear)


def tint(name, hexcol):
    """A finish like `name` in another colour."""
    h, d, s, m, dirt, wear, param, glow = MATS[name]
    return sk.fin('f-%s-%06x' % (name, hexcol), hexcol, d, s, m, dirt, 0.0, glow, param, wear)


def lamp(hexcol, gain=2.0):
    return sk.fin('f-lamp-%06x-%d' % (hexcol, int(gain * 10)), hexcol, A.DETAIL_GLOW, .5, 0, 0, 0, gain, 0, 0)


def ext(b):
    p, s = b['pos'], b['size']
    return p[0] - s[0] / 2, p[0] + s[0] / 2, p[1] - s[1] / 2, p[1] + s[1] / 2, p[2] - s[2] / 2, p[2] + s[2] / 2


# ---------------------------------------------------------------------------------------------------------------------------
# The half turn: the south / B pieces are the north / A ones turned half round about the map's centre
# ---------------------------------------------------------------------------------------------------------------------------
HALF = (-1.0, 0, 0, 0, 0, 1.0, 0, 0, 0, 0, -1.0, 0)


def turned(i):
    """Whether block (or owner) id `i` is a south / B piece (modelled as its twin and turned half round)."""
    for t in i.split(':')[-1].split('-'):
        t = t.rstrip('0123456789')
        if t in ('n', 'a'):
            return False
        if t in ('s', 'b'):
            return True
    return False


def twin_block(b):
    p = b['pos']
    return dict(b, pos=[-p[0], p[1], -p[2]])


def twin_part(p):
    c, q = p['c'], p['q']
    # the part turned half round about y: q' = qY * q with qY = (0, 1, 0, 0)
    x, y, z, w = q
    return dict(p, c=[-c[0], c[1], -c[2]], q=[z, w, -x, -y])


def twin_hazard(h):
    def t(v):
        return [-v[0], v[1], -v[2]]
    return dict(h, a=t(h['a']), b=t(h['b']), zones=[dict(z, center=t(z['center'])) for z in h.get('zones', [])])


# ---------------------------------------------------------------------------------------------------------------------------
# Shapes
# ---------------------------------------------------------------------------------------------------------------------------
def bx(g, x0, x1, y0, y1, z0, z1, f, T, bevel=.02, cell=0, skip=()):
    box(g, x0, x1, y0, y1, z0, z1, f, T, bevel, cell, skip)


def cyl(g, p0, p1, r, f, n, caps=True, r1=None):
    sk.cylinder(g, p0, p1, r, f, max(3, n), caps, r1)


def tube(g, path, r, f, n=6, closed=False, caps=True):
    sk.tube(g, path, r, f, max(3, n), closed=closed, caps=caps)


def isection(w, h, tf, tw):
    a, b = w / 2, h / 2
    return [(-a, -b), (a, -b), (a, -b + tf), (tw / 2, -b + tf), (tw / 2, b - tf), (a, b - tf), (a, b), (-a, b), (-a, b - tf), (-tw / 2, b - tf),
            (-tw / 2, -b + tf), (-a, -b + tf)]


def ibeam(g, p0, p1, w, h, f, T, tf=None, tw=None, up=None):
    """An I-section from p0 to p1: w across (the frame's u), h along `up`: two flanges and the web, each its own bar (so every
    end is a plain rectangle). The Low tier draws its box."""
    tf = tf or max(.02, h * .1)
    tw = tw or max(.015, w * .12)
    up = up or ((0, 1, 0) if abs(p1[1] - p0[1]) < .95 * math.dist(p0, p1) else (1, 0, 0))
    if T.i == 2:
        sk.sweep(g, sk.rect_section(w, h), [p0, p1], f, up=up, smooth_u=False)
        return
    t = sk._norm(sk._sub(p1, p0))
    u = sk._norm(sk._cross(up, t))
    v = sk._cross(t, u)
    for off, sec in (((h - tf) / 2, sk.rect_section(w, tf)), (-(h - tf) / 2, sk.rect_section(w, tf)), (0.0, sk.rect_section(tw, h - 2 * tf))):
        sk.sweep(g, sec, [sk._add(p0, v, off), sk._add(p1, v, off)], f, up=up, smooth_u=False)


def bar(g, p0, p1, w, h, f, T, chamfer=.008, up=None):
    """A flat bar or box section from p0 to p1 (w across, h along up)."""
    sec = sk.rect_section(w, h, chamfer if T.i == 0 else 0.0)
    sk.sweep(g, sec, [p0, p1], f, up=up or ((0, 1, 0) if abs(p1[1] - p0[1]) < .95 * math.dist(p0, p1) else (1, 0, 0)), smooth_u=T.i == 0 and chamfer > 0)


def disc(g, c, r, f, n=12, normal=Y):
    u, v, w = sk.frame((0, 0, 0), normal)
    g.face([sk._add(sk._add(c, u, r * math.cos(2 * math.pi * k / n)), v, r * math.sin(2 * math.pi * k / n)) for k in range(n)], f, normal=normal)


def ring(g, c, r0, r1, f, n=16, normal=Y):
    """A flat annulus (r0 inner, r1 outer) facing `normal`."""
    u, v, w = sk.frame((0, 0, 0), normal)

    def P(r, k):
        a = 2 * math.pi * k / n
        return sk._add(sk._add(c, u, r * math.cos(a)), v, r * math.sin(a))
    for k in range(n):
        g.face((P(r0, k), P(r1, k), P(r1, k + 1), P(r0, k + 1)), f, normal=normal)


def bolts(g, pts, f, T, r=.022, h=.022, axis=Y):
    """Bolt heads (hexagons) at the points, standing h along the axis. Fine tier only."""
    if not T.tiny:
        return
    for p in pts:
        cyl(g, p, sk._add(p, axis, h), r, f, 6)


def _clip(poly, a, b, c):
    """The part of a 2D polygon where a x + b y <= c."""
    out = []
    n = len(poly)
    for i in range(n):
        p, q = poly[i], poly[(i + 1) % n]
        dp, dq = a * p[0] + b * p[1] - c, a * q[0] + b * q[1] - c
        if dp <= 0:
            out.append(p)
        if (dp < 0 < dq) or (dq < 0 < dp):
            t = dp / (dp - dq)
            out.append((p[0] + (q[0] - p[0]) * t, p[1] + (q[1] - p[1]) * t))
    return out


def stripes(g, o, U, V, lu, lv, normal, band=.2, fa=None, fb=None, slant=1.0, phase=0.0):
    """Hazard stripes on the rectangle o + s U + t V (U, V unit; lu x lv m): slanted bands, yellow and black."""
    fa = fa if fa is not None else mat('hazard-y')
    fb = fb if fb is not None else mat('hazard-k')
    rect = [(0, 0), (lu, 0), (lu, lv), (0, lv)]
    # bands of constant (s - slant t): from -slant lv to lu
    k0 = int(math.floor((-abs(slant) * lv - phase) / band)) - 1
    k1 = int(math.ceil((lu + abs(slant) * lv - phase) / band)) + 1
    for k in range(k0, k1):
        lo, hi = phase + k * band, phase + (k + 1) * band
        poly = _clip(_clip(rect, 1, -slant, hi), -1, slant, -lo)
        if len(poly) >= 3:
            g.face([sk._add(sk._add(o, U, p[0]), V, p[1]) for p in poly], fa if k % 2 == 0 else fb, normal=normal)


def striped_box(g, x0, x1, y0, y1, z0, z1, T, band=.2, skip=()):
    """A box painted in hazard stripes on its four sides and its top (the Low tier: plain yellow; a face too narrow for a
    stripe: plain yellow; paint on a floor: its top only)."""
    if T.i == 2 and (y1 - y0 <= .03 or max(x1 - x0, y1 - y0, z1 - z0) < .9):
        bx(g, x0, x1, y0, y1, z0, z1, mat('hazard-y'), T, 0, 0, skip)
        return

    def side(o, U, V, lu, lv, n):
        if min(lu, lv) < (.05 if T.i < 2 else .25):
            g.face((o, sk._add(o, U, lu), sk._add(sk._add(o, U, lu), V, lv), sk._add(o, V, lv)), mat('hazard-y'), normal=n)
        else:
            stripes(g, o, U, V, lu, lv, n, min(.3, max(.1, min(lu, lv) * .5)) * (1, 2, 3)[T.i])
    flat = y1 - y0 <= .03
    if not flat:
        if '-z' not in skip:
            side((x0, y0, z0), (1, 0, 0), Y, x1 - x0, y1 - y0, (0, 0, -1))
        if '+z' not in skip:
            side((x0, y0, z1), (1, 0, 0), Y, x1 - x0, y1 - y0, (0, 0, 1))
        if '-x' not in skip:
            side((x0, y0, z0), (0, 0, 1), Y, z1 - z0, y1 - y0, (-1, 0, 0))
        if '+x' not in skip:
            side((x1, y0, z0), (0, 0, 1), Y, z1 - z0, y1 - y0, (1, 0, 0))
    if '+y' not in skip:
        if x1 - x0 >= z1 - z0:
            side((x0, y1, z0), (1, 0, 0), (0, 0, 1), x1 - x0, z1 - z0, Y)
        else:
            side((x0, y1, z0), (0, 0, 1), (1, 0, 0), z1 - z0, x1 - x0, Y)
    if not flat and '-y' not in skip and y0 > .05:
        g.face(((x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)), mat('hazard-k'), normal=(0, -1, 0))


def corrugated(g, a, b, y0, y1, n, f, T, pitch=.36, depth=.035, rows=5.0):
    """Corrugated sheet in the vertical plane through the plan points a -> b (x, z), from y0 to y1, its ribs upright, facing the
    plan normal n (x, z). Trapezoid ribs on the Fine tier, wider on the Mid, flat on the Low."""
    L = math.hypot(b[0] - a[0], b[1] - a[1])
    if L < 1e-6:
        return
    ux, uz = (b[0] - a[0]) / L, (b[1] - a[1]) / L
    nr = max(1, int(math.ceil((y1 - y0) / rows))) if T.i == 0 else 1
    if T.i == 2:
        cnt = max(1, int(math.ceil(L / 6.0)))
        for i in range(cnt):
            s0, s1 = L * i / cnt, L * (i + 1) / cnt
            g.face(((a[0] + ux * s0, y0, a[1] + uz * s0), (a[0] + ux * s1, y0, a[1] + uz * s1), (a[0] + ux * s1, y1, a[1] + uz * s1), (a[0] + ux * s0, y1, a[1] + uz * s0)), f,
                   normal=(n[0], 0, n[1]))
        return
    p = pitch * (1.2 if T.i == 0 else 3)
    cnt = max(1, int(round(L / p)))
    p = L / cnt
    prof = [(0.0, 0.0), (.3, 0.0), (.5, 1.0), (.8, 1.0), (1.0, 0.0)]
    for i in range(cnt):
        for j in range(len(prof) - 1):
            (s0, d0), (s1, d1) = prof[j], prof[j + 1]
            sa, sb = (i + s0) * p, (i + s1) * p
            da, db = d0 * depth, d1 * depth
            pa = (a[0] + ux * sa + n[0] * da, a[1] + uz * sa + n[1] * da)
            pb = (a[0] + ux * sb + n[0] * db, a[1] + uz * sb + n[1] * db)
            # the strip's own normal: n turned by its slope
            tx, tz = pb[0] - pa[0], pb[1] - pa[1]
            l = math.hypot(tx, tz)
            fnx, fnz = -tz / l, tx / l
            if fnx * n[0] + fnz * n[1] < 0:
                fnx, fnz = -fnx, -fnz
            for r in range(nr):
                ya, yb = y0 + (y1 - y0) * r / nr, y0 + (y1 - y0) * (r + 1) / nr
                g.face(((pa[0], ya, pa[1]), (pb[0], ya, pb[1]), (pb[0], yb, pb[1]), (pa[0], yb, pa[1])), f, normal=(fnx, 0, fnz))


def corrugated_top(g, x0, x1, z0, z1, y, f, T, up=True, pitch=.3, depth=.03, along_x=True):
    """Corrugated sheet lying flat at height y over the rectangle (its ribs along z when along_x: the profile runs along x)."""
    if T.i == 2:
        g.face(((x0, y, z0), (x1, y, z0), (x1, y, z1), (x0, y, z1)), f, normal=(0, 1 if up else -1, 0))
        return
    s = 1 if up else -1
    L = (x1 - x0) if along_x else (z1 - z0)
    p = pitch * (1 if T.i == 0 else 2.5)
    cnt = max(1, int(round(L / p)))
    p = L / cnt
    prof = [(0.0, 0.0), (.3, 0.0), (.5, 1.0), (.8, 1.0), (1.0, 0.0)]
    for i in range(cnt):
        for j in range(len(prof) - 1):
            (s0, d0), (s1, d1) = prof[j], prof[j + 1]
            ua, ub = (i + s0) * p, (i + s1) * p
            ya, yb = y + s * d0 * depth, y + s * d1 * depth
            l = math.hypot(ub - ua, yb - ya)
            nu, ny = -(yb - ya) / l * s, (ub - ua) / l * s
            if along_x:
                g.face(((x0 + ua, ya, z0), (x0 + ub, yb, z0), (x0 + ub, yb, z1), (x0 + ua, ya, z1)), f, normal=(nu, ny, 0))
            else:
                g.face(((x0, ya, z0 + ua), (x0, yb, z0 + ub), (x1, yb, z0 + ub), (x1, ya, z0 + ua)), f, normal=(0, ny, nu))


def rotate(q, v):
    x, y, z, w = q
    tx, ty, tz = 2 * (y * v[2] - z * v[1]), 2 * (z * v[0] - x * v[2]), 2 * (x * v[1] - y * v[0])
    return (v[0] + w * tx + (y * tz - z * ty), v[1] + w * ty + (z * tx - x * tz), v[2] + w * tz + (x * ty - y * tx))


def moved(g, t, q=(0, 0, 0, 1)):
    return g.transformed(sk.affine(q, t))


def kname(p):
    return KINDS[p['k']]


ROUND = ('rail', 'cable', 'pipe-steam', 'pipe-water', 'pipe-gas', 'pipe-air', 'chrome')
GLOW_KIND = ('lamp',)


def kit_part(g, p, T, f=None):
    """A kit part rebuilt as a finished piece: a long bar of a round kind becomes a tube, a flange a short turned disc, a wheel a
    tyre, a stripe part painted stripes, a lamp a glowing lens, a decal a flat plate, anything else a chamfered box."""
    name = kname(p)
    c, h, q = p['c'], p['h'], p['q']
    order = sorted(range(3), key=lambda i: h[i])
    lo, mid, hi = h[order[0]], h[order[1]], h[order[2]]
    axes = [rotate(q, (1, 0, 0)), rotate(q, (0, 1, 0)), rotate(q, (0, 0, 1))]
    if f is None:
        if name == 'lamp':
            f = lamp(p['col'], 2.0)
        elif name in ('window', 'glass'):
            f = mat('daylight')
        elif name in KIND_MAT:
            f = mat(KIND_MAT[name])
            if name in ('door', 'sign', 'panel', 'bin', 'box', 'machine') and p['col'] != KCOL[p['k']]:
                f = tint(KIND_MAT[name], p['col'])
        else:
            f = tint('steel', p['col'])
    if hi < .07 and not T.tiny:
        return                  # the smallest parts are the Fine tier's
    if T.i == 2 and (hi < .16 or (name == 'rail' and not (hi > .3 and abs(h[1] - .025) < .002)) or name in ('toe', 'joint', 'flange', 'valve') or (name == 'frame' and hi < 2)
                     or (name == 'truss' and mid < .055) or (name in ('bracket', 'black') and hi < .4)):
        return                  # the Low tier keeps the top rails and the larger pieces
    if T.i == 1 and ((name == 'truss' and mid < .045) or (name == 'bracket' and hi < .12) or (name == 'frame' and hi < .6) or (name == 'black' and hi < .2) or name == 'toe'
                     or (name == 'rail' and (abs(h[1] - .0175) < .002 or (h[1] > .3 and h[0] < .03 and h[2] < .03)))):
        return                  # the Mid tier leaves out the trusses' diagonals, the lanterns' short bars, the knee rails
    if name == 'stripe':
        if abs(q[3]) > .999 and T.i < 2:
            striped_box(g, c[0] - h[0], c[0] + h[0], c[1] - h[1], c[1] + h[1], c[2] - h[2], c[2] + h[2], T, skip=('-y',) if c[1] - h[1] < .02 else ())
        else:
            obox(g, c, h, q, mat('hazard-y'), T, 0)
        return
    if name in ROUND and hi > 3 * mid and mid < .2 and mid <= lo * 1.6:
        ax = axes[order[2]]
        r = (lo + mid) / 2
        cyl(g, sk._add(c, ax, -hi), sk._add(c, ax, hi), r, f, (6, 4, 4)[T.i] if r < .04 else T.small if r < .09 else T.round, caps=name != 'rail')
        return
    if name == 'flange' or (name == 'valve' and lo < .08 and mid > 1.5 * lo):
        ax = axes[order[0]]
        cyl(g, sk._add(c, ax, -lo), sk._add(c, ax, lo), (mid + hi) / 2, f, T.small + 2)
        return
    if name == 'wheel':
        ax = axes[order[0]]
        r = mid * 1.3
        cyl(g, sk._add(c, ax, -lo), sk._add(c, ax, lo), r, f, T.round + 2)
        cyl(g, sk._add(c, ax, -lo - .012), sk._add(c, ax, lo + .012), r * .45, mat('yellow'), T.round)
        return
    if lo <= .0125 or T.i > 0 or name not in ('machine', 'panel', 'concrete', 'motor', 'crane', 'crane-dark', 'ram', 'ladle', 'ladle-rim', 'sign', 'door') or mid < .04:
        # a decal, a thin plate or a slender bar: unbevelled
        kbox(g, c, h, q, f, T, 0)
        return
    kbox(g, c, h, q, f, T, .02)


def kbox(g, c, h, q, f, T, bevel):
    """A kit part's turned box: its faces cut into cells only where they are broad (a long bar is one face a side)."""
    t = Geo()
    broad = sorted(h)[1] > .6
    # a part standing on the slab has no underside
    skip = ('-y',) if (abs(q[3]) > .9999 and c[1] - h[1] < .02) else ()
    box(t, -h[0], h[0], -h[1], h[1], -h[2], h[2], f, T, bevel, cell=(2.0 if broad and T.i == 0 else 0), skip=skip)
    g.extend(t if (abs(q[3]) > .999999 and c == (0, 0, 0)) else t.transformed(sk.affine(q, c)))


def body(g, b, f, T, bevel=.03, skip=('-y',), cell=None):
    """A block's own box in a finish, chamfered, without its underside when it stands on the floor."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    box(g, x0, x1, y0, y1, z0, z1, f, T, bevel, cell=(T.cell if T.i == 0 else 0) if cell is None else cell, skip=skip if y0 < .05 else ())
