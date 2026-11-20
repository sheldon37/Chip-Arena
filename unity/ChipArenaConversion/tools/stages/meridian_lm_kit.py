"""Meridian landmarks: the shared pieces (tiers, the layout's blocks, extra finishes, lamps, brackets, lattices, windows).

Built on the Meridian kit (meridian_gates_kit: finishes F, glow, bx, wall/Hole/reveal, mould, text_stroke, clock at 6:47, lamps) and
stagekit's corner geometry, in Unity space (metres, +y up). A facade built here faces -z unless a frame turns it; `placed`
builds a sub-geometry in that canonical frame and turns it onto any face.
"""
import math

import meridian_gates_kit as mk
import stagekit as sk
from meridian_gates_kit import F, Geo, glow

PI = math.pi

# The night's lights (art bible 3.1-3.2)
SODIUM, SODIUM_GLASS = 0xFFA14A, 0xFFC27A
WARM, ROOM, CURTAIN = 0xFFB062, 0xFFD08A, 0xE07A50
RED_LIGHT = 0xFF3A28
OPAL = 0xF2E6C6
WHITE_GOLD = 0xFFE6A6


def _cell_at(T, y):
    """The vertex-colour cell for the landmarks: they are read from 20 m and more, so LOD0 takes 1.5 m cells in the actor band and
    3 m above 6 m (the street kit's 0.75 / 1.5 m), LOD1 and the far layer 10 m."""
    if T.lod:
        return 10.0
    return 1.5 if y < 6.0 else 3.0


mk.cell_at = _cell_at
_BX = mk.bx


def _bx(g, T, x0, x1, y0, y1, z0, z1, f, bevel=.02, cell=None, skip=()):
    """The kit's box, without chamfers on small parts (under 1.2 m every way): a chamfered box is 44 triangles, a plain one 12,
    and at the landmarks' read distances a small part's chamfer is under a pixel."""
    dims = (abs(x1 - x0), abs(y1 - y0), abs(z1 - z0))
    if max(dims) < 1.2:
        bevel = 0.0
    if cell is None and min(dims) < .6:
        cell = 0          # a band, a plinth or a coping: its length needs no vertex-colour cells
    return _BX(g, T, x0, x1, y0, y1, z0, z1, f, bevel=bevel, cell=cell, skip=skip)


mk.bx = _bx
bx = _bx


def tier(t):
    """0 Fine (LOD0), 2 Low (LOD1), 'far' (the far layer: the Low tier's counts, no bevels)."""
    T = mk.tier(0 if t == 0 else 1)
    T.cell = 1.5 if t == 0 else 10.0
    if t == 0:
        T.arc = 8          # (an arched window's head is cut into T.arc strips: 8 reads as round from 20 m)
    T.far = t == 'far'
    return T


_EXTRA = {}


def finishes():
    """The landmarks' own finishes on top of the Meridian table (bible 4.2's families)."""
    if _EXTRA:
        return _EXTRA
    a = mk._add_fin
    D = sk
    _EXTRA['stucco-hotel'] = a('stucco-hotel', 0xE6DCC4, D.DETAIL_PLASTER, .12, 0, .9, .1, .1)
    _EXTRA['iron-lattice'] = a('iron-lattice', 0x2E3A40, D.DETAIL_IRON, .40, .5, .8, 0, .30)
    _EXTRA['gilt'] = a('gilt', 0xD4A84A, D.DETAIL_BRASS, .75, 1.0, .3, 0, .35)
    _EXTRA['stone-black'] = a('stone-black', 0x26252C, D.DETAIL_FLAG, .45, 0, .6, .05, .2, .8)
    _EXTRA['marble-white'] = a('marble-white', 0xE8E4DC, D.DETAIL_MARBLE, .55, 0, .5, 0, .15)
    _EXTRA['concrete-coal'] = a('concrete-coal', 0x7A7872, D.DETAIL_PLASTER, .1, 0, 1.6, .2, .1)
    _EXTRA['linen'] = a('linen', 0xE2DCCB, D.DETAIL_CLOTH, .1, 0, .5, 0, 0)
    _EXTRA['linen-blue'] = a('linen-blue', 0x6E86A8, D.DETAIL_CLOTH, .1, 0, .5, 0, 0)
    _EXTRA['linen-red'] = a('linen-red', 0xA8564E, D.DETAIL_CLOTH, .1, 0, .5, 0, 0)
    _EXTRA['timber-tar'] = a('timber-tar', 0x3A2E26, D.DETAIL_WOOD, .2, 0, 1.0, .2, .1)
    _EXTRA['timber-oak'] = a('timber-oak', 0x6A5038, D.DETAIL_WOOD, .25, 0, .9, .1, .15)
    _EXTRA['cinema-cream'] = a('cinema-cream', 0xE8D8B4, D.DETAIL_TILE, .55, 0, .5, 0, .05, .3)
    _EXTRA['cinema-red'] = a('cinema-red', 0x8C1E28, D.DETAIL_NONE, .55, 0, .5, 0, .2)
    _EXTRA['poster-paper'] = a('poster-paper', 0xD8CCA8, D.DETAIL_NONE, .1, 0, .6, 0, 0)
    _EXTRA['white-cladding-far'] = a('white-cladding-far', 0xDCE6EA, D.DETAIL_NONE, .4, 0, .5, 0, 0)
    return _EXTRA


def floors_of(g):
    """The floors for the bake's grime (the lowest point of the piece and every 7 m step under it)."""
    import numpy as np
    P = np.frombuffer(g.p, dtype=np.float32).reshape(-1, 3)
    lo = float(P[:, 1].min()) if len(P) else 0.0
    return tuple(sorted({round(lo, 2)} | {float(y) for y in (-14.0, -7.0, 0.0, 7.0, 14.0, 21.0, 28.0) if y >= lo - .1}))


class Site:
    """A layout's blocks by id (x0, x1, y0, y1, z0, z1)."""

    def __init__(self, L):
        self.L = L
        self.b = {b[0]: tuple(b[1:7]) for b in L['blocks']}
        self.seed = L['seed']

    def __getitem__(self, k):
        return self.b[k]

    def get(self, k):
        return self.b.get(k)


# ---------------------------------------------------------------------------------------------------------------------------
# Frames
# ---------------------------------------------------------------------------------------------------------------------------
def placed(g, build, yaw=0.0, t=(0.0, 0.0, 0.0)):
    """Builds `build(sub)` in the canonical frame (facing -z) and turns it by yaw degrees about +y, then moves it to t.
    yaw 0 faces -z, 90 faces -x (Unity: a positive yaw turns +z toward +x, so -z toward -x), 180 faces +z, 270 faces +x."""
    sub = Geo()
    build(sub)
    mk.part(g, sub, mk.qy(yaw), t)


def facing_yaw(n):
    """The yaw that turns the canonical -z facade to face the unit direction n (axis aligned)."""
    nx, nz = n
    if nz < -.5:
        return 0.0
    if nx < -.5:
        return 90.0
    if nz > .5:
        return 180.0
    return 270.0


# ---------------------------------------------------------------------------------------------------------------------------
# Lamps and their hardware
# ---------------------------------------------------------------------------------------------------------------------------
def lantern(g, T, x, y, z, s=1.0, glass=None, body=None, hang=0.0):
    """A heritage lantern (bible 6.2's square-tapered head) hanging with its top at (x, y, z): a cap, four glass panes glowing
    sodium, a base and a finial; hang > 0 adds a chain above it."""
    body = F['iron-black'] if body is None else body
    glass = glow(SODIUM_GLASS, 2.6) if glass is None else glass
    w0, w1, h = .26 * s, .36 * s, .62 * s
    yb = y - h
    sides = 4
    # the glass: a tapered square frustum, wider at the top
    sk.lathe(g, [(w0 * 1.414, 0.0), (w1 * 1.414, h * .78)], (x, yb, z), glass, sides, smooth=False, phase=PI / 4, cap0=True)
    # the cap: a pyramid with an eave
    sk.lathe(g, [(w1 * 1.6, h * .78), (w1 * 1.6, h * .84), (w1 * .5, h * .98), (.02 * s, h * 1.08)], (x, yb, z), body, sides, smooth=False, phase=PI / 4)
    # the base and the corner bars
    sk.lathe(g, [(w0 * 1.1, -h * .1), (w0 * 1.5, -h * .02), (w0 * 1.5, 0.0)], (x, yb, z), body, sides, smooth=False, phase=PI / 4, cap0=True)
    if T.detail:
        for k in range(4):
            a = PI / 4 + k * PI / 2
            mk.rod(g, (x + math.cos(a) * w0 * 1.414, yb, z + math.sin(a) * w0 * 1.414), (x + math.cos(a) * w1 * 1.414, yb + h * .78, z + math.sin(a) * w1 * 1.414),
                   .018 * s, body, 4, caps=False)
        sk.lathe(g, [(.05 * s, -h * .1), (.05 * s, -h * .2), (0.0, -h * .3)], (x, yb, z), body, 4, smooth=False)
    if hang > 0:
        n = max(1, int(hang / .25)) if T.detail else 1
        for i in range(n):
            a = y + h * .08 + hang * i / n
            mk.rod(g, (x, a, z), (x, a + hang / n * .9, z), .02 * s, body, 4, caps=False)


def globe(g, T, x, y, z, r=.45, f=None):
    """A glowing opal globe hanging by a rod from y."""
    f = glow(0xFFE2B0, 2.2) if f is None else f
    mk.ellipsoid(g, (x, y - r * 1.2, z), (r, r), f, T.round, rings=4 if T.detail else 3)
    mk.rod(g, (x, y - r * .3, z), (x, y, z), .03, F['brass'], 4, caps=False)
    sk.lathe(g, [(r * .45, 0), (r * .35, r * .25)], (x, y - r * .45, z), F['brass'], T.small, smooth=False)


def scroll_bracket(g, T, a, b, f, w=.08, curl=.35, simple=False):
    """A wrought scroll bracket from the wall point a out to the tip b (in a vertical plane): a top bar, a lower curved
    stay and a scroll at the root."""
    ax, ay, az = a
    bx_, by, bz = b
    mk.seg(g, T, (ax, ay, az), (bx_, by, bz), w, w, f)
    n = 6 if (T.detail and not simple) else 3
    # the stay: a quarter arc from the wall below up to two thirds of the arm
    pts = []
    for i in range(n + 1):
        t = i / n
        ang = t * PI / 2
        px = ax + (bx_ - ax) * .66 * math.sin(ang)
        pz = az + (bz - az) * .66 * math.sin(ang)
        py = ay - curl * 2.2 * (1 - (1 - math.cos(ang)))
        pts.append((px, py + (by - ay) * .66 * math.sin(ang), pz))
    for p, q in zip(pts[:-1], pts[1:]):
        mk.seg(g, T, p, q, w * .8, w * .8, f)
    if T.detail and not simple:
        # the root scroll: a small spiral of bars
        cx, cy, cz = ax + (bx_ - ax) * .12, ay - curl * .9, az + (bz - az) * .12
        m = 8
        prev = None
        for i in range(m + 1):
            ang = i / m * 2 * PI
            r = curl * .45 * (1 - i / (m * 1.3))
            dx, dz = (bx_ - ax), (bz - az)
            L = math.hypot(dx, dz) or 1
            p = (cx + dx / L * r * math.cos(ang), cy + r * math.sin(ang), cz + dz / L * r * math.cos(ang))
            if prev:
                mk.seg(g, T, prev, p, w * .6, w * .6, f)
            prev = p


def lattice(g, T, a, b, up, depth, f, bays=None, w=.12):
    """A flat lattice girder from a to b: two chords `depth` apart along `up`, verticals and zigzag diagonals."""
    L = math.dist(a, b)
    n = bays or max(2, int(L / (depth * 1.1)))
    a2 = tuple(a[i] + up[i] * depth for i in range(3))
    b2 = tuple(b[i] + up[i] * depth for i in range(3))
    mk.seg(g, T, a, b, w, w * 1.4, f)
    mk.seg(g, T, a2, b2, w, w * 1.4, f)
    if not T.detail and not getattr(T, 'far', False) and n > 6:
        n = max(3, n // 2)
    for i in range(n + 1):
        t = i / n
        p = tuple(a[k] + (b[k] - a[k]) * t for k in range(3))
        q = tuple(p[k] + up[k] * depth for k in range(3))
        mk.seg(g, T, p, q, w * .7, w * .7, f)
        if i < n:
            t2 = (i + 1) / n
            p2 = tuple(a[k] + (b[k] - a[k]) * t2 for k in range(3))
            q2 = tuple(p2[k] + up[k] * depth for k in range(3))
            if i % 2 == 0:
                mk.seg(g, T, p, q2, w * .6, w * .6, f)
            else:
                mk.seg(g, T, q, p2, w * .6, w * .6, f)


def arc_points(cx, cy, r, a0, a1, n):
    return [(cx + r * math.cos(a0 + (a1 - a0) * i / n), cy + r * math.sin(a0 + (a1 - a0) * i / n)) for i in range(n + 1)]


def window(g, T, x0, x1, y0, y1, z, lit, f_frame, rise=0.0, f_dark=None, bars=True):
    """A window's glazing in the plane z (facing -z): a lit pane or dark glass, an arched head of `rise`, and its bars. The
    wall's hole and reveal are the caller's (facade does both)."""
    pane = glow(lit[0], lit[1]) if lit else (F['glass-dark'] if f_dark is None else f_dark)
    h = mk.Hole(x0, x1, y0, y1, rise)
    if rise > 0:
        k = max(3, T.arc)
        pts = [(x0, y0), (x1, y0)] + [(x1 - (x1 - x0) * i / k, h.top(x1 - (x1 - x0) * i / k)) for i in range(k + 1)]
        g.face([(p[0], p[1], z) for p in pts], pane, (0, 0, -1))
    else:
        g.face(((x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z)), pane, (0, 0, -1))
    if bars and lit and T.detail and x1 - x0 > .5:   # (a dark pane's bars do not read at night)
        zb = z - .02
        cx = (x0 + x1) / 2
        mk.seg(g, T, (cx, y0, zb), (cx, h.top(cx), zb), .05, .04, f_frame)
        mk.seg(g, T, (x0, (y0 + y1) / 2, zb), (x1, (y0 + y1) / 2, zb), .05, .04, f_frame)


def facade(g, T, z, x0, x1, y0, y1, f, wins=(), f_frame=None, reveal=.15, f_sill=None, flat=False):
    """A wall in the plane z facing -z, x0..x1 by y0..y1, with windows wins [(x0, x1, y0, y1, lit or None[, rise])]: their reveals
    `reveal` deep, the glazing at the reveal's back, sills proud (LOD0). flat (and the Low tier): the windows are panes laid on
    the wall instead (the faces seen only from far off)."""
    f_frame = F['paint-cream'] if f_frame is None else f_frame
    if flat or not T.detail:
        mk.wall(g, T, z, x0, x1, y0, y1, f, holes=(), n=-1)
        for w in wins:
            h = mk.Hole(w[0], w[1], w[2], w[3], w[5] if len(w) > 5 else 0.0)
            window(g, T, w[0], w[1], w[2], w[3], z - .015, w[4], f_frame, rise=h.rise, bars=False)
        return
    holes = [mk.Hole(w[0], w[1], w[2], w[3], w[5] if len(w) > 5 else 0.0) for w in wins]
    mk.wall(g, T, z, x0, x1, y0, y1, f, holes=holes, n=-1)
    for w, h in zip(wins, holes):
        mk.reveal(g, T, h, z, z + reveal, f, sill=True, head=True)
        window(g, T, w[0], w[1], w[2], w[3], z + reveal, w[4], f_frame, rise=h.rise)
        if T.detail and w[2] > .5:
            bx(g, T, w[0] - .08, w[1] + .08, w[2] - .1, w[2], z - .1, z, F['stone-dress'] if f_sill is None else f_sill, bevel=.01, cell=0)


def lit_or_dark(seed, i, share=.35, hexes=(WARM, ROOM, CURTAIN), gain=1.5):
    h = mk.h01(seed, i)
    if h < share:
        return (hexes[int(mk.h01(seed, i, 7) * len(hexes)) % len(hexes)], gain * (.8 + .4 * mk.h01(seed, i, 3)))
    return None


def corbel(g, T, x, y, z, f, w=.5, d=.6, h=.7, n=-1):
    """A stone corbel on a wall at (x, y, z) (y its top), projecting d along the wall's normal n (z axis)."""
    zz = z + n * d
    pts = [(x - w / 2, y, z), (x + w / 2, y, z), (x + w / 2, y, zz), (x - w / 2, y, zz)]
    bot = [(x - w / 2, y - h, z), (x + w / 2, y - h, z)]
    g.face(pts, f, (0, 1, 0))
    g.face((pts[3], pts[2], bot[1], bot[0]), f, mk._norm((0, -d, n * h)) if False else (0, -.6, n * .8))
    g.face((pts[0], pts[3], bot[0]), f, (-1, 0, 0))
    g.face((pts[1], bot[1], pts[2]), f, (1, 0, 0))


# ---------------------------------------------------------------------------------------------------------------------------
# Skins: a block's faces dressed as a building
# ---------------------------------------------------------------------------------------------------------------------------
FACES = ('s', 'n', 'w', 'e')


def face_frame(box, side):
    """(yaw, t, width) that turn a canonical facade (x 0..width along the face, facing -z at z = 0, y absolute) onto a block's
    side: 's' the -z face, 'n' +z, 'w' -x, 'e' +x."""
    x0, x1, y0, y1, z0, z1 = box
    if side == 's':
        return 0.0, (x0, 0.0, z0), x1 - x0
    if side == 'n':
        return 180.0, (x1, 0.0, z1), x1 - x0
    if side == 'w':
        return 90.0, (x0, 0.0, z1), z1 - z0
    return 270.0, (x1, 0.0, z0), z1 - z0


def skin(g, box, build, sides=FACES):
    """Builds build(sub, side, width) on each side of the block in the canonical frame and turns it into place."""
    for s in sides:
        yaw, t, w = face_frame(box, s)
        placed(g, lambda sub, s=s, w=w: build(sub, s, w), yaw, t)


def roof_cap(g, T, box, f, inset=0.0, y=None):
    """The block's top as a flat roof surface (5 mm over the collider top)."""
    x0, x1, y0, y1, z0, z1 = box
    yy = (y1 if y is None else y) + .005
    mk.ground(g, T, x0 + inset, x1 - inset, z0 + inset, z1 - inset, yy, f, cell=3.0 if T.lod == 0 else 10.0)


def cornice(g, T, y, x0, x1, z, f, s=1.0, closed_ends=True):
    """A cornice moulding along a canonical face (facing -z at plane z), its top at y (it projects about 0.4 m: above 3.1 m)."""
    prof = [(0, 0), (.12 * s, .06 * s), (.16 * s, .2 * s), (.42 * s, .3 * s), (.42 * s, .42 * s), (0, .42 * s)] if T.detail else \
        [(0, 0), (.42 * s, .3 * s), (.42 * s, .42 * s), (0, .42 * s)]
    mk.mould(g, T, prof, [(x0, z), (x1, z)], f, y=y - .42 * s, caps=(closed_ends, closed_ends), cell=0)


def string_course(g, T, y, x0, x1, z, f, h=.18, d=.08):
    bx(g, T, x0, x1, y, y + h, z - d, z, f, bevel=.01, cell=0, skip=('+z',))


def keep_faces(g, pred):
    """The faces of g whose corners' mean satisfies pred(mean) (x, y, z), as a new Geo."""
    import array
    import numpy as np
    P, N, S, Fc = g.arrays()
    starts = np.zeros(len(S), dtype=np.int64)
    if len(S) > 1:
        starts[1:] = np.cumsum(S[:-1])
    sel = [i for i in range(len(S)) if pred(P[starts[i]:starts[i] + S[i]].mean(axis=0))]
    out = Geo()
    if not sel:
        return out
    sel = np.array(sel)
    counts = S[sel]
    cidx = np.repeat(starts[sel], counts) + (np.arange(counts.sum()) - np.repeat(np.cumsum(counts) - counts, counts))
    out.p = array.array('f')
    out.p.frombytes(P[cidx].astype(np.float32).tobytes())
    out.n = array.array('f')
    out.n.frombytes(N[cidx].astype(np.float32).tobytes())
    out.size = array.array('B')
    out.size.frombytes(S[sel].astype(np.uint8).tobytes())
    out.fin = array.array('H')
    out.fin.frombytes(Fc[sel].astype(np.uint16).tobytes())
    return out


def half_lathe(g, prof, x, z, f, sides, smooth=True):
    """The front half (z < 0 side) of a lathe about the vertical axis through (x, z): an engaged column's shaft on a face."""
    sub = Geo()
    sk.lathe(sub, prof, (0, 0, 0), f, sides, smooth=smooth)
    half = keep_faces(sub, lambda c: c[2] < .02)
    mk.part(g, half, (0, 0, 0, 1), (x, 0.0, z))
