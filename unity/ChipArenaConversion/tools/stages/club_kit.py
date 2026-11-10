"""Substation 128's art: what its three scripts share (club_art.py runs them): the finishes, the kit kinds' finishes, and the
modelling helpers on stagekit (rounded boxes, tubes, I-beams, rivets, wall planes with arched openings, lofted skins).
Everything is in the game's space (Unity: metres, +y up; +z is north, the stage; -z south, the bar)."""
import math

import numpy as np

import stagekit as sk
from stagekit import Geo, box, obox, sweep, lathe, grid_face, cylinder

F = {}            # finishes by name
KIND = []         # kit kind index -> finish name (None: a glow kind, by the part's own colour)
Y = (0.0, 1.0, 0.0)
PARTY = (0x22E6FF, 0x8B5CFF, 0xFF3DCF, 0x3D6BFF, 0xFF6FD8, 0x3DFFD2, 0xC8F4FF)
CYAN, VIOLET, MAGENTA = 0x22E6FF, 0x8B5CFF, 0xFF3DCF


def finishes(L):
    A = sk

    def add(name, hexcol, detail=A.DETAIL_NONE, **kw):
        F[name] = A.fin(name, hexcol, detail, **kw)
    # the shell
    add('brick', 0x8A4A3C, A.DETAIL_ASHLAR, smooth=.14, param=.21, dirt=1.0)
    add('brickdark', 0x5A3230, A.DETAIL_ASHLAR, smooth=.12, param=.21, dirt=1.0)
    add('brickup', 0x6A3A34, A.DETAIL_ASHLAR, smooth=.12, param=.21, dirt=.8)
    add('stone', 0x8E8884, A.DETAIL_CARVED, smooth=.22, wear=.25)
    add('concrete', 0x3C3848, A.DETAIL_FLAG, smooth=.66, param=2.4, dirt=.5)
    add('slab', 0x2C2A34, A.DETAIL_PLASTER, smooth=.2, dirt=.6)
    add('plaster', 0x403A50, A.DETAIL_PLASTER, smooth=.16, dirt=.7)
    add('dado', 0x2A2636, A.DETAIL_PLASTER, smooth=.3, dirt=.6)
    add('glassfloor', 0x0C0A14, smooth=.9, dirt=.1)
    # metals
    add('steel', 0x545868, A.DETAIL_IRON, smooth=.42, metal=.8, wear=.4, dirt=.8)
    add('steeldark', 0x2A2A34, A.DETAIL_IRON, smooth=.4, metal=.75, wear=.35, dirt=.7)
    add('gunmetal', 0x3A3644, A.DETAIL_BRASS, smooth=.55, metal=.9, wear=.3, dirt=.5)
    add('truss', 0x9A9EAC, A.DETAIL_BRASS, smooth=.74, metal=1.0, dirt=.3)
    add('alu', 0x8A8E9A, A.DETAIL_BRASS, smooth=.7, metal=1.0, dirt=.3, wear=.2)
    add('chrome', 0xC8CCD8, smooth=.93, metal=1.0, dirt=.15)
    add('brass', 0xC8A040, A.DETAIL_BRASS, smooth=.8, metal=1.0, dirt=.3, wear=.2)
    add('copper', 0xB8683C, A.DETAIL_BRASS, smooth=.72, metal=1.0, dirt=.4, wear=.2)
    add('mirror', 0xE0E4F0, smooth=.86, metal=1.0, dirt=.1)
    add('porcelain', 0xD8D2C4, A.DETAIL_WAX, smooth=.72, dirt=.5)
    add('porcelaindark', 0x6A4A3A, A.DETAIL_WAX, smooth=.72, dirt=.5)
    add('crane', 0xA8862C, A.DETAIL_IRON, smooth=.38, metal=.5, wear=.4, dirt=1.0)
    add('ironroof', 0x3A3844, A.DETAIL_IRON, smooth=.35, metal=.7, wear=.3)
    add('roof', 0x1E1A26, A.DETAIL_PLASTER, smooth=.1, dirt=.4)
    add('keg', 0x9298A0, A.DETAIL_BRASS, smooth=.62, metal=1.0, dirt=.5, wear=.3)
    # blacks
    add('black', 0x15141A, A.DETAIL_PLASTER, smooth=.3, dirt=.3)
    add('blackgloss', 0x0E0D12, A.DETAIL_MARBLE, smooth=.86, dirt=.15)
    add('case', 0x18181E, smooth=.42, dirt=.4, wear=.2)
    add('rubber', 0x18181A, A.DETAIL_PLASTER, smooth=.16, dirt=.5)
    add('grille', 0x1E1E26, A.DETAIL_CLOTH, smooth=.3, metal=.4, dirt=.3)
    add('cone', 0x0E0E12, A.DETAIL_PLASTER, smooth=.22, dirt=.2)
    add('screenoff', 0x08080E, smooth=.88, dirt=.1)
    add('glass', 0x1A1C34, smooth=.94, dirt=.12)
    # soft and warm
    add('velvet', 0x7A1648, A.DETAIL_CLOTH, smooth=.14, dirt=.3)
    add('velvetdark', 0x4A0E30, A.DETAIL_CLOTH, smooth=.12, dirt=.3)
    add('velvetblue', 0x36206C, A.DETAIL_CLOTH, smooth=.14, dirt=.3)
    add('velvetgreen', 0x3A2A68, A.DETAIL_CLOTH, smooth=.14, dirt=.3)
    add('leather', 0x241014, A.DETAIL_PLASTER, smooth=.5, dirt=.3)
    for axis, tag in ((0, 'x'), (1, 'y'), (2, 'z')):
        add('wood' + tag, 0x6A3620, A.DETAIL_WOOD, smooth=.5, param=axis, wear=.25, dirt=.6)
        add('wooddark' + tag, 0x3E2218, A.DETAIL_WOOD, smooth=.44, param=axis, wear=.2, dirt=.6)
        add('pine' + tag, 0x9A7448, A.DETAIL_WOOD, smooth=.3, param=axis, wear=.2, dirt=.9)
    add('paint', 0xD8D4E8, smooth=.3, dirt=.6)
    add('hazard', 0xF4B321, smooth=.34, dirt=.7)
    add('leaf', 0x2E6E42, A.DETAIL_WAX, smooth=.4, dirt=.2)
    add('soil', 0x1E1612, A.DETAIL_PLASTER, smooth=.08)
    # the Greys
    add('skin0', 0x8E949A, A.DETAIL_WAX, smooth=.46, dirt=.12)
    add('skin1', 0x9EAC98, A.DETAIL_WAX, smooth=.46, dirt=.12)
    add('skin2', 0x7C8AA4, A.DETAIL_WAX, smooth=.46, dirt=.12)
    add('skin3', 0xB6BABC, A.DETAIL_WAX, smooth=.46, dirt=.12)
    add('skindj', 0xA6B4B0, A.DETAIL_WAX, smooth=.5, dirt=.1)
    add('eye', 0x040408, smooth=.98, dirt=0)
    add('slit', 0x1E2024, smooth=.3, dirt=0)
    add('suit', 0x1A1A2A, A.DETAIL_CLOTH, smooth=.4, metal=.3, dirt=.2)
    add('suitsilver', 0xAEB4C4, A.DETAIL_CLOTH, smooth=.7, metal=.8, dirt=.2)
    add('vest', 0xE8EA40, A.DETAIL_CLOTH, smooth=.3, dirt=.2)
    add('jacket', 0x2A1450, A.DETAIL_CLOTH, smooth=.55, metal=.5, dirt=.15)
    names = ['black', 'gunmetal', 'chrome', None, None, 'cone', 'glass', 'velvet', 'woodx', 'truss', None, 'black', 'mirror', 'paint', 'brass', None,
             'rubber', 'concrete', 'brick', None, 'grille', 'black', 'leaf', 'stone', 'steel', 'hazard']
    KIND.extend(names)


def glow(hexcol, gain):
    """A glowing finish of this colour and HDR gain (its own sub-mesh; the bloom takes gains above 1)."""
    gain = round(max(.4, gain), 2)
    name = 'glow-%06x-%s' % (hexcol & 0xFFFFFF, gain)
    if name not in F:
        F[name] = sk.fin(name, hexcol & 0xFFFFFF, sk.DETAIL_GLOW, glow=gain)
    return F[name]


def kit_fin(p, L):
    """A kit part's finish: its kind's, or for the lit kinds (LED, screen, bottle, lens, sign) a glow of the part's colour."""
    name = KIND[p['k']]
    if name is not None:
        return F[name]
    col = p['col'] if p['col'] >= 0 else L['kindColors'][p['k']]
    return glow(col, p['w'] if p['w'] > 1 else 1.0)


def ext(b):
    p, s = b['pos'], b['size']
    return (p[0] - s[0] / 2, p[0] + s[0] / 2, p[1] - s[1] / 2, p[1] + s[1] / 2, p[2] - s[2] / 2, p[2] + s[2] / 2)


def rotate(q, v):
    x, y, z, w = q
    tx, ty, tz = 2 * (y * v[2] - z * v[1]), 2 * (z * v[0] - x * v[2]), 2 * (x * v[1] - y * v[0])
    return (v[0] + w * tx + (y * tz - z * ty), v[1] + w * ty + (z * tx - x * tz), v[2] + w * tz + (x * ty - y * tx))


# ---------------------------------------------------------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------------------------------------------------------
def rod(g, a, b, r, f, sides, caps=True):
    if sk._dot(sk._sub(b, a), sk._sub(b, a)) < 1e-10:
        return
    cylinder(g, a, b, r, f, sides, caps=caps)


def rbox(g, x0, x1, y0, y1, z0, z1, r, f, segs=2):
    """A box with every edge and corner rounded to radius r (cushions, cases, cabinets): each face a grid whose rim rolls round
    the edge; smooth normals."""
    r = max(1e-4, min(r, (x1 - x0) * .49, (y1 - y0) * .49, (z1 - z0) * .49))
    lo, hi = (x0, y0, z0), (x1, y1, z1)
    offs = [r * (1 - math.tan(math.pi / 4 * (1 - j / segs))) for j in range(segs + 1)]      # 0 .. r from the face's edge

    def coords(a):
        return [lo[a] + o for o in offs] + [hi[a] - o for o in reversed(offs)]

    def proj(p):
        inner = tuple(min(max(p[a], lo[a] + r), hi[a] - r) for a in range(3))
        d = sk._sub(p, inner)
        n = sk._norm(d)
        return sk._add(inner, n, r), n
    for a in range(3):
        b, c = (a + 1) % 3, (a + 2) % 3
        cb, cc = coords(b), coords(c)
        for side in (0, 1):
            va = lo[a] if side == 0 else hi[a]
            for i in range(len(cb) - 1):
                for j in range(len(cc) - 1):
                    pts, nrm = [], []
                    for (bi, cj) in ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1)):
                        p = [0.0, 0.0, 0.0]
                        p[a], p[b], p[c] = va, cb[bi], cc[cj]
                        P, N = proj(tuple(p))
                        pts.append(P)
                        nrm.append(N)
                    g.face(pts, f, normals=nrm)


def ibeam(g, path, h, w, f, up=None, t=None, caps=False):
    """An I-section (h tall across the frame's v, w wide) swept along a path; flat faces."""
    t = t or min(h, w) * .16
    a, b = w / 2, h / 2
    sec = [(-a, -b), (a, -b), (a, -b + t), (t / 2, -b + t), (t / 2, b - t), (a, b - t), (a, b), (-a, b), (-a, b - t), (-t / 2, b - t), (-t / 2, -b + t), (-a, -b + t)]
    sweep(g, sec, path, f, up=up, caps=caps, smooth_u=False)


def rivets(g, pts, n, r, f):
    """Rivet heads at the points on a face of normal n: low four-sided domes (smooth normals), Fine tier only."""
    u, v, w = sk.frame((0, 0, 0), n)
    for p in pts:
        top = sk._add(p, w, r * .6)
        ring = [sk._add(sk._add(p, u, r * math.cos(a)), v, r * math.sin(a)) for a in (0, math.pi / 2, math.pi, 3 * math.pi / 2)]
        rn = [sk._norm(sk._add(sk._sub(q, p), w, r * .5)) for q in ring]
        for i in range(4):
            g.face((ring[i], ring[(i + 1) % 4], top), f, normals=(rn[i], rn[(i + 1) % 4], w))


def disc(g, c, r, f, sides, normal):
    """A flat disc facing `normal`."""
    u, v, w = sk.frame((0, 0, 0), normal)
    g.face([sk._add(sk._add(c, u, r * math.cos(2 * math.pi * i / sides)), v, r * math.sin(2 * math.pi * i / sides)) for i in range(sides)], f, normal)


def lathe2(g, profile, centre, f, sides, axis=Y):
    """A thin turned shell seen from both sides (a lamp's shade, a horn)."""
    lathe(g, profile, centre, f, sides, axis=axis)
    lathe(g, profile[::-1], centre, f, sides, axis=axis)


def dome(g, c, r, f, sides, rings=3, axis=Y, squash=1.0):
    """A hemisphere (or flattened dome) on a plane through c with its pole along `axis`."""
    prof = [(r * math.cos(math.pi / 2 * i / rings), r * squash * math.sin(math.pi / 2 * i / rings)) for i in range(rings + 1)]
    prof[-1] = (1e-4, r * squash)
    lathe(g, prof, c, f, sides, axis=axis)


def ball(g, c, r, f, sides, rings):
    prof = [(max(1e-4, r * math.sin(math.pi * i / rings)), -r * math.cos(math.pi * i / rings)) for i in range(rings + 1)]
    lathe(g, prof, c, f, sides)


class Plane:
    """A wall's face: origin at u 0 on the floor, u along the wall (a horizontal unit), n the face's normal (into the room)."""

    def __init__(self, origin, ud, n):
        self.o, self.ud, self.n = origin, ud, n

    def at(self, u, y, d=0.0):
        return (self.o[0] + self.ud[0] * u + self.n[0] * d, y, self.o[2] + self.ud[2] * u + self.n[2] * d)

    def box(self, g, u0, u1, y0, y1, d0, d1, f, T=None, bevel=.01, cell=0):
        a, b = self.at(u0, y0, d0), self.at(u1, y1, d1)
        box(g, a[0], b[0], a[1], b[1], a[2], b[2], f, T, bevel, cell=cell)


def wall(g, pl, u0, u1, y0, y1, f, T, cell=None):
    if u1 - u0 < 1e-6 or y1 - y0 < 1e-6:
        return
    grid_face(g, pl.at(u0, y0), (pl.ud[0] * (u1 - u0), 0, pl.ud[2] * (u1 - u0)), (0, y1 - y0, 0), 0, 0, f, pl.n, cell or T.cell)


def arcade(g, pl, u0, u1, y0, y1, ops, sill, spring, apex, f, T, thick=.4, ring=None, stone=None, cell=None):
    """A wall face with arched openings ops [(a, b)] (sill, springing and apex heights shared): the face in four bands, the
    openings' reveals (jambs, sill, soffit) `thick` deep, a ring of voussoirs round each (finish `ring`) and a stone sill."""
    wall(g, pl, u0, u1, y0, sill, f, T, cell)
    wall(g, pl, u0, u1, apex, y1, f, T, cell)
    edges = [u0] + [e for o in ops for e in o] + [u1]
    for i in range(0, len(edges), 2):
        wall(g, pl, edges[i], edges[i + 1], sill, apex, f, T, cell)
    n = max(4, T.arc // 2 * 2)
    for (a, b) in ops:
        half, rise = (b - a) / 2, apex - spring
        us = [a + (b - a) * i / n for i in range(n + 1)]
        hs = [spring + sk.arch_height(half, rise, min(u - a, b - u)) for u in us]
        centre = pl.at((a + b) / 2, spring - half, -thick / 2)
        for i in range(n):
            g.face((pl.at(us[i], hs[i]), pl.at(us[i + 1], hs[i + 1]), pl.at(us[i + 1], apex), pl.at(us[i], apex)), f, pl.n)
            mid = pl.at((us[i] + us[i + 1]) / 2, (hs[i] + hs[i + 1]) / 2, -thick / 2)
            g.face((pl.at(us[i], hs[i]), pl.at(us[i + 1], hs[i + 1]), pl.at(us[i + 1], hs[i + 1], -thick), pl.at(us[i], hs[i], -thick)), f, sk._sub(centre, mid))
        for (u, s) in ((a, 1.0), (b, -1.0)):
            rows = max(1, int((spring - sill) / (cell or T.cell)))
            for k in range(rows):
                ya, yb = sill + (spring - sill) * k / rows, sill + (spring - sill) * (k + 1) / rows
                g.face((pl.at(u, ya), pl.at(u, yb), pl.at(u, yb, -thick), pl.at(u, ya, -thick)), f, (pl.ud[0] * s, 0, pl.ud[2] * s))
        if stone is not None:
            pl.box(g, a - .12, b + .12, sill - .14, sill, -thick, .07, stone, T, bevel=.015)
        if ring is not None:
            w = .32
            pts2 = [(a, sill), (a, spring)] + [(us[i], hs[i]) for i in range(1, n)] + [(b, spring), (b, sill)]
            cx, cy = (a + b) / 2, sill
            path = []
            for i, (u, y) in enumerate(pts2):
                pa, pb = pts2[max(0, i - 1)], pts2[min(len(pts2) - 1, i + 1)]
                tx, ty = pb[0] - pa[0], pb[1] - pa[1]
                l = math.hypot(tx, ty) or 1.0
                nx, ny = ty / l, -tx / l
                if nx * (u - cx) + ny * (y - cy) < 0:
                    nx, ny = -nx, -ny
                path.append(pl.at(u + nx * w / 2, y + ny * w / 2, 0))
            sweep(g, sk.rect_section(w, .09), path, ring, path_normals=[pl.n] * len(path), smooth_u=False, caps=False)
            if stone is not None and T.detail:
                pl.box(g, (a + b) / 2 - .16, (a + b) / 2 + .16, apex - .02, apex + .42, -.02, .09, stone, T, bevel=.02)


def kit_part(g, p, T, L, f=None):
    """A kit part rebuilt as a finished piece: a bar becomes a round tube, a lit strip a plain glowing slab, anything else a
    chamfered box in its finish."""
    hx, hy, hz = p['h']
    h = (hx, hy, hz)
    lo, hi = min(h), max(h)
    if T.i == 2 and hi < .06:
        return
    if T.i == 2 and p['f'] in ('truss', 'tower') and h[1] < .02 and (int(p['c'][0] * 7 + p['c'][2] * 13 + p['c'][1] * 5) & 1):
        return
    f = kit_fin(p, L) if f is None else f
    lit = sk.FINS[f].glow > 0
    order = sorted(range(3), key=lambda i: h[i])
    a, b, c = order
    if not lit and abs(h[a] - h[b]) < .006 and h[c] > 2.5 * h[b] and h[b] < .09:
        ax = [0.0, 0.0, 0.0]
        ax[c] = h[c]
        d = rotate(p['q'], tuple(ax))
        sides = ((4, 3, 3)[T.i] if h[b] < .02 else (6, 4, 3)[T.i] if h[b] < .05 else (T.round, 6, 4)[T.i])
        rod(g, sk._sub(p['c'], d), sk._add(p['c'], d), h[b] * 1.12, f, sides, caps=T.i == 0 and h[b] > .03)
        return
    if sk.FINS[f].detail == sk.DETAIL_WOOD:
        axis = int(np.argmax(np.abs(rotate(p['q'], tuple(1.0 if i == c else 0.0 for i in range(3))))))
        f = F[sk.FINS[f].name[:-1] + 'xyz'[axis]]
    t = Geo()
    bevel = 0.0 if (lit or lo < .012 or T.i == 2) else min(.02, lo * .35)
    box(t, -hx, hx, -hy, hy, -hz, hz, f, T if bevel > 0 else None, bevel, cell=0)
    g.extend(t.transformed(sk.affine(p['q'], p['c'])))


def skin(g, rings, f, close0=True, close1=True):
    """A lofted skin through rings of points (each the same count, wound the same way), with smooth normals from the grid's
    own neighbours; the ends closed by fans to the ring's centre. For limbs, bodies and heads."""
    m, k = len(rings), len(rings[0])
    R = np.array(rings, dtype=np.float64)                    # m, k, 3
    du = np.roll(R, -1, axis=1) - np.roll(R, 1, axis=1)
    dv = np.empty_like(R)
    dv[1:-1] = R[2:] - R[:-2]
    dv[0] = R[1] - R[0]
    dv[-1] = R[-1] - R[-2]
    N = np.cross(du, dv)
    ctr = R.mean(axis=1, keepdims=True)
    flip = np.sign(np.sum(N * (R - ctr), axis=2, keepdims=True).sum(axis=1, keepdims=True))
    flip[flip == 0] = 1
    N = N * flip
    N /= np.maximum(np.linalg.norm(N, axis=2, keepdims=True), 1e-12)
    for i in range(m - 1):
        for j in range(k):
            j1 = (j + 1) % k
            g.face((tuple(R[i, j]), tuple(R[i, j1]), tuple(R[i + 1, j1]), tuple(R[i + 1, j])), f,
                   normals=(tuple(N[i, j]), tuple(N[i, j1]), tuple(N[i + 1, j1]), tuple(N[i + 1, j])))
    for (i, on, s) in ((0, close0, -1.0), (m - 1, close1, 1.0)):
        if not on:
            continue
        c = tuple(ctr[i, 0])
        axis = sk._norm(tuple((ctr[i, 0] - ctr[i + (1 if i == 0 else -1), 0])))
        for j in range(k):
            j1 = (j + 1) % k
            g.face((tuple(R[i, j]), tuple(R[i, j1]), c), f, normals=(tuple(N[i, j]), tuple(N[i, j1]), axis))


def ring(c, rx, rz, n, y=None, tilt=None, shift=(0, 0), power=2.0):
    """A ring of n points round c in the xz plane: a superellipse of half axes rx (x) and rz (z), from +x counter-clockwise
    seen from above. tilt (a 3x3 rows matrix) turns it about c; shift moves its middle."""
    out = []
    for i in range(n):
        a = 2 * math.pi * i / n
        ca, sa = math.cos(a), math.sin(a)
        e = 2.0 / power
        px = rx * (abs(ca) ** e) * (1 if ca >= 0 else -1) + shift[0]
        pz = rz * (abs(sa) ** e) * (1 if sa >= 0 else -1) + shift[1]
        v = (px, 0.0, pz)
        if tilt is not None:
            v = tuple(tilt[r][0] * v[0] + tilt[r][1] * v[1] + tilt[r][2] * v[2] for r in range(3))
        out.append((c[0] + v[0], c[1] + v[1], c[2] + v[2]))
    return out


def limb(g, pts, radii, f, n, flat=1.0, close0=True, close1=True, side=(1, 0, 0)):
    """A limb through points with a radius at each (round, or flattened by `flat` across `side`): rings square to the path."""
    rings = []
    m = len(pts)
    for i in range(m):
        a, b = pts[max(0, i - 1)], pts[min(m - 1, i + 1)]
        w = sk._norm(sk._sub(b, a))
        u = sk._norm(sk._sub(side, tuple(w[j] * sk._dot(side, w) for j in range(3))))
        v = sk._cross(w, u)
        r = radii[i]
        rings.append([sk._add(sk._add(pts[i], u, r * math.cos(2 * math.pi * j / n)), v, r * flat * math.sin(2 * math.pi * j / n)) for j in range(n)])
    skin(g, rings, f, close0, close1)


def mat_rot(axis, angle):
    x, y, z = sk._norm(axis)
    c, s = math.cos(angle), math.sin(angle)
    return ((c + x * x * (1 - c), x * y * (1 - c) - z * s, x * z * (1 - c) + y * s),
            (y * x * (1 - c) + z * s, c + y * y * (1 - c), y * z * (1 - c) - x * s),
            (z * x * (1 - c) - y * s, z * y * (1 - c) + x * s, c + z * z * (1 - c)))
