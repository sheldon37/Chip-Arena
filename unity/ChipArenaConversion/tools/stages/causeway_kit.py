"""Starfall Causeway's modelling helpers (causeway_art.py, causeway_props.py and causeway_void.py build on them; stagekit.py is
the shared kit): its finishes, a wall face's frame for relief (mouldings, blind arches, buttresses, rune bands), surfaces lofted
along a spine (robes, wings, limbs, legs), chiselled rocks, broken masonry (fracture faces, toothed ends), runes and floor inlays.
Everything is in the game's space (Unity: metres, +y up)."""
import math

import numpy as np

import stagekit as sk
from stagekit import Geo, box, sweep, lathe, grid_face

Y = (0.0, 1.0, 0.0)
F = {}


def finishes():
    A = sk
    F['flag'] = A.fin('cw-flag', 0xCDBFA8, A.DETAIL_FLAG, smooth=.3)
    F['ashlar'] = A.fin('cw-ashlar', 0xC6B9A3, A.DETAIL_ASHLAR, smooth=.18, param=1.2)
    F['keel'] = A.fin('cw-keel', 0xB0A38F, A.DETAIL_ASHLAR, smooth=.16, param=1.6)
    F['carved'] = A.fin('cw-carved', 0xDCD3C2, A.DETAIL_CARVED, smooth=.24, wear=.3)
    F['stone'] = A.fin('cw-stone', 0xCFC4AE, A.DETAIL_CARVED, smooth=.2, wear=.25)
    F['deep'] = A.fin('cw-deep', 0x877F78, A.DETAIL_CARVED, smooth=.12, dirt=.7)
    F['shade'] = A.fin('cw-shade', 0x4A4658, A.DETAIL_PLASTER, smooth=.08, dirt=.5)
    F['broken'] = A.fin('cw-broken', 0x9A8E80, A.DETAIL_CARVED, smooth=.1, dirt=.8)
    F['statue'] = A.fin('cw-statue', 0xDCD2C0, A.DETAIL_CARVED, smooth=.3, wear=.3)
    F['metal'] = A.fin('cw-metal', 0x3E4A72, A.DETAIL_BRASS, smooth=.72, metal=.9, dirt=.3, wear=.45)
    F['silver'] = A.fin('cw-silver', 0xA9B8DA, A.DETAIL_BRASS, smooth=.8, metal=1.0, dirt=.2)
    F['gold'] = A.fin('cw-gold', 0xD8B060, A.DETAIL_BRASS, smooth=.8, metal=1.0, dirt=.25, wear=.2)
    F['bronze'] = A.fin('cw-bronze', 0xA07A48, A.DETAIL_BRASS, smooth=.55, metal=1.0, dirt=.5)
    F['rock'] = A.fin('cw-rock', 0x4E4656, A.DETAIL_NONE, smooth=.1, dirt=.6)
    F['rockpale'] = A.fin('cw-rockpale', 0x8D8276, A.DETAIL_NONE, smooth=.12, dirt=.7)
    F['rubble'] = A.fin('cw-rubble', 0xB9AD98, A.DETAIL_NONE, smooth=.14, dirt=.8)
    F['cloth'] = A.fin('cw-cloth', 0x3C55A8, A.DETAIL_CLOTH, smooth=.25, dirt=.4)
    F['clothedge'] = A.fin('cw-clothedge', 0xC8962E, A.DETAIL_BRASS, smooth=.6, metal=.8, dirt=.3)
    F['ice'] = A.fin('cw-ice', 0x56708C, A.DETAIL_NONE, smooth=.7, dirt=.3)
    F['scorch'] = A.fin('cw-scorch', 0x2A2226, A.DETAIL_NONE, smooth=.1, dirt=.3)
    # the lights (the glow sub-mesh; gain above 1 blooms)
    F['rune'] = A.fin('cw-rune', 0xFFB45A, glow=1.7)
    F['runedim'] = A.fin('cw-runedim', 0xC88236, glow=.55)
    F['cold'] = A.fin('cw-cold', 0x8FE6FF, glow=1.9)
    F['crystal'] = A.fin('cw-crystal', 0x7FE0FF, glow=2.3)
    F['violet'] = A.fin('cw-violet', 0xB48CFF, glow=2.1)
    F['lamp'] = A.fin('cw-lamp', 0xFFD9A0, glow=2.5)
    F['ember'] = A.fin('cw-ember', 0xFF8A3A, glow=3.0)
    F['white'] = A.fin('cw-white', 0xFFFFFF, glow=1.0)
    F['comet'] = A.fin('cw-comet', 0xCFF4FF, glow=2.6)


def ext(b):
    p, s = b['pos'], b['size']
    return (p[0] - s[0] / 2, p[0] + s[0] / 2, p[1] - s[1] / 2, p[1] + s[1] / 2, p[2] - s[2] / 2, p[2] + s[2] / 2)


def blk(x0, x1, y0, y1, z0, z1, i=''):
    return {'id': i, 'pos': [(x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2], 'size': [x1 - x0, y1 - y0, z1 - z0]}


def hashf(*k):
    """A stable 0..1 hash of a few numbers."""
    h = 2166136261
    for v in k:
        h = (h ^ (int(round(v * 1000)) & 0xFFFFFFFF)) * 16777619 & 0xFFFFFFFF
        h ^= h >> 13
    h = (h * 2654435761) & 0xFFFFFFFF
    return ((h >> 8) & 0xFFFF) / 65535.0


def facet(g, pts, f, hint):
    """A flat face with its own geometric normal, turned to agree with `hint` (chiselled stone)."""
    n = (0.0, 0.0, 0.0)
    for i in range(1, len(pts) - 1):
        n = sk._add(n, sk._cross(sk._sub(pts[i], pts[0]), sk._sub(pts[i + 1], pts[0])))
    if n[0] * n[0] + n[1] * n[1] + n[2] * n[2] < 1e-14:
        return
    if sk._dot(n, hint) < 0:
        n = (-n[0], -n[1], -n[2])
    g.face(tuple(pts), f, normal=n)


def facet2(g, a, b, c, d, f, hint):
    """A quad as two flat triangles."""
    facet(g, (a, b, c), f, hint)
    facet(g, (a, c, d), f, hint)


def simp(profile, T):
    """A moulding's section for the Low tier: its bounding slab."""
    if T.i < 2:
        return profile
    u0, u1 = min(p[0] for p in profile), max(p[0] for p in profile)
    o = max(p[1] for p in profile) * .8
    return [(u0, -.01), (u0, o), (u1, o), (u1, -.01)]


def ribbon(g, section, path, nrm, f, closed=False):
    """An open section [(x, y)] swept along a path lying in a plane with normal `nrm`: x across the path in the plane (to the
    left of its direction seen against the normal... the side n x T), y out of the plane. Only the section's own strips are
    built (no back, no caps): a moulding lying on a wall."""
    m = len(path)
    rings, frames = [], []
    for i in range(m):
        a = path[(i - 1) % m] if (closed or i > 0) else path[i]
        b = path[(i + 1) % m] if (closed or i < m - 1) else path[i]
        t = sk._norm(sk._sub(b, a))
        u = sk._norm(sk._cross(nrm, t))
        frames.append(u)
        rings.append([sk._add(sk._add(path[i], u, x), nrm, y) for x, y in section])
    cxs = sum(p[0] for p in section) / len(section)
    for j in range(len(section) - 1):
        dx, dy = section[j + 1][0] - section[j][0], section[j + 1][1] - section[j][1]
        sx, sy = dy, -dx
        if sy < -1e-9 or (abs(sy) <= 1e-9 and sx * ((section[j][0] + section[j + 1][0]) / 2 - cxs) < 0):
            sx, sy = -sx, -sy
        for i in range(m if closed else m - 1):
            i1 = (i + 1) % m
            n0 = sk._norm(sk._add(sk._lerp((0, 0, 0), frames[i], sx), nrm, sy))
            n1 = sk._norm(sk._add(sk._lerp((0, 0, 0), frames[i1], sx), nrm, sy))
            g.face((rings[i][j], rings[i][j + 1], rings[i1][j + 1], rings[i1][j]), f, normals=(n0, n0, n1, n1))


# ---------------------------------------------------------------------------------------------------------------------------
# A wall face's frame: a along it (u = Y x n), y the world's, d out of it
# ---------------------------------------------------------------------------------------------------------------------------
class Face:
    def __init__(self, o, n, length):
        self.o, self.n, self.L = o, n, length
        self.u = (n[2], 0.0, -n[0])

    def P(self, a, y, d=0.0):
        o, u, n = self.o, self.u, self.n
        return (o[0] + u[0] * a + n[0] * d, y, o[2] + u[2] * a + n[2] * d)

    def key(self, name):
        n, u = self.n, self.u
        ax = 'x' if abs(n[0]) > .5 else 'z'
        s = n[0] if ax == 'x' else n[2]
        au = 'x' if abs(u[0]) > .5 else 'z'
        su = u[0] if au == 'x' else u[2]
        return {'back': ('-' if s > 0 else '+') + ax, 'front': ('+' if s > 0 else '-') + ax, 'lo': ('-' if su > 0 else '+') + au,
                'hi': ('+' if su > 0 else '-') + au, 'bottom': '-y', 'top': '+y'}[name]

    def box(self, g, a0, a1, y0, y1, d0, d1, f, T, bevel=.02, cell=0, skip=('back',)):
        p, q = self.P(a0, y0, d0), self.P(a1, y1, d1)
        if T is not None and T.i > 0:
            bevel = 0
        box(g, min(p[0], q[0]), max(p[0], q[0]), y0, y1, min(p[2], q[2]), max(p[2], q[2]), f, T, bevel, cell, tuple(self.key(k) for k in skip))

    def quad(self, g, a0, a1, y0, y1, d, f, cell=0):
        if cell:
            grid_face(g, self.P(a0, y0, d), sk._sub(self.P(a1, y0, d), self.P(a0, y0, d)), (0, y1 - y0, 0), 0, 0, f, self.n, cell)
        else:
            g.face((self.P(a0, y0, d), self.P(a1, y0, d), self.P(a1, y1, d), self.P(a0, y1, d)), f, self.n)

    def mould(self, g, profile, y, a0, a1, f, T, d=0.0, caps=True):
        """A moulding along the face: profile [(up, out)] from the path at height y."""
        sweep(g, simp(profile, T), [self.P(a0, y, d), self.P(a1, y, d)], f, path_normals=[self.n, self.n], smooth_u=False, caps=caps)

    def arch_path(self, ac, half, ys, rise, n, d=0.0):
        h = sk.pointed_arch(half, rise, n)
        pts = [self.P(ac - half + o, ys + y, d) for o, y in h] + [self.P(ac + half - o, ys + y, d) for o, y in h[-2::-1]]
        return pts

    def arch(self, g, ac, half, ys, rise, section, f, T, d=0.0, legs=0.0, n=None):
        """An archivolt: `section` [(outward from the opening, out of the wall)] swept round the arch (and down its legs)."""
        n = max(3, T.arc // 4) if n is None else n
        pts = self.arch_path(ac, half, ys, rise, n, d)
        if legs > 0:
            pts = [self.P(ac - half, ys - legs, d)] + pts + [self.P(ac + half, ys - legs, d)]
        ribbon(g, section if T.i == 0 or len(section) < 5 else [section[0], section[2], section[-2], section[-1]], pts, self.n, f)

    def arch_fill(self, g, ac, half, y0, ys, rise, f, T, d=.004, n=None):
        """The field under an arch (down to y0), flat at depth d."""
        n = max(3, T.arc // 3) if n is None else n
        h = sk.pointed_arch(half, rise, n)
        if ys > y0:
            g.face((self.P(ac - half, y0, d), self.P(ac + half, y0, d), self.P(ac + half, ys, d), self.P(ac - half, ys, d)), f, self.n)
        for i in range(n):
            (o0, h0), (o1, h1) = h[i], h[i + 1]
            g.face((self.P(ac - half + o0, ys + h0, d), self.P(ac + half - o0, ys + h0, d), self.P(ac + half - o1, ys + h1, d), self.P(ac - half + o1, ys + h1, d)), f, self.n)

    def soffit(self, g, ac, half, ys, rise, depth, f, T, n=None):
        """An arch's underside through a wall `depth` thick (from the face inward)."""
        n = max(3, T.arc // 3) if n is None else n
        h = sk.pointed_arch(half, rise, n)
        for s in (-1, 1):
            for i in range(n):
                (o0, h0), (o1, h1) = h[i], h[i + 1]
                a0, a1 = ac + s * (half - o0), ac + s * (half - o1)
                nr = sk._add(sk._lerp((0, 0, 0), self.u, -s * (h1 - h0)), Y, -(o1 - o0) - 1e-6)
                self_n = sk._norm(nr)
                g.face((self.P(a0, ys + h0, 0), self.P(a1, ys + h1, 0), self.P(a1, ys + h1, -depth), self.P(a0, ys + h0, -depth)), f, normal=self_n)

    def spandrel(self, g, ac, half, ys, rise, ytop, f, T, d=0.0, n=None):
        """The wall between an arch and the square head over it (both haunches)."""
        n = max(3, T.arc // 3) if n is None else n
        h = sk.pointed_arch(half, rise, n)
        for i in range(n):
            (o0, h0), (o1, h1) = h[i], h[i + 1]
            for s in (-1, 1):
                a0, a1 = ac + s * (half - o0), ac + s * (half - o1)
                g.face((self.P(a0, ys + h0, d), self.P(a1, ys + h1, d), self.P(a1, ytop, d), self.P(a0, ytop, d)), f, self.n)

    def buttress(self, g, a, w, steps, f, T, cap=None):
        """A stepped buttress across a +- w/2: steps [(y0, y1, out)] bottom to top, each set-off weathered (sloped) into the
        next; `cap` the finish of the weatherings."""
        cf = f if cap is None else cap
        for i, (y0, y1, out) in enumerate(steps):
            nxt = steps[i + 1][2] if i + 1 < len(steps) else 0.0
            slope = min(.35, (out - nxt) * 1.2) if out > nxt else 0.0
            self.box(g, a - w / 2, a + w / 2, y0, y1 - slope, 0, out, f, T, bevel=.025, skip=('back', 'top') if slope > 0 else ('back',))
            if slope > 0:
                A, B = self.P(a - w / 2, y1 - slope, out), self.P(a + w / 2, y1 - slope, out)
                C, D = self.P(a + w / 2, y1, nxt), self.P(a - w / 2, y1, nxt)
                g.face((A, B, C, D), cf, normal=sk._add(sk._lerp((0, 0, 0), self.n, slope), Y, out - nxt))
                for s, a1 in ((-1, a - w / 2), (1, a + w / 2)):
                    g.face((self.P(a1, y1 - slope, nxt), self.P(a1, y1 - slope, out), self.P(a1, y1, nxt)), f, normal=sk._lerp((0, 0, 0), self.u, s))

    def runes(self, g, a0, a1, y, h, T, f, seed, d=.006):
        """A band of rune glyphs (the Low tier: one strip)."""
        if T.i == 2:
            self.quad(g, a0, a1, y - h * .12, y + h * .12, d + .02, f)
            return
        pitch = h * 1.05
        n = max(1, int((a1 - a0) / pitch))
        a = (a0 + a1) / 2 - n * pitch / 2 + pitch / 2
        for i in range(n):
            glyph(g, lambda x, v, a=a: self.P(a + x * h * .62, y + v * h, d), self.n, seed * 131 + i, h * .085, f, 3 if T.fine else 2)
            a += pitch

    def roundel(self, g, a, y, r, T, f_ring, f_star=None, d=0.0, points=8):
        n = max(8, T.arc * 2 // 3)
        path = [self.P(a + math.cos(2 * math.pi * k / n) * r, y + math.sin(2 * math.pi * k / n) * r, d) for k in range(n)]
        ribbon(g, [(-.035, 0), (0, .05), (.035, 0)] if T.i else [(-.04, 0), (-.02, .045), (.02, .045), (.04, 0)], path, self.n, f_ring, closed=True)
        if f_star is not None:
            star(g, lambda x, v: self.P(a + x, y + v, d + .008), self.n, r * .82, r * .3, points, f_star)


# ---------------------------------------------------------------------------------------------------------------------------
# Runes, stars, rings (flat inlays through a map M(x, y) -> point on a plane with normal n)
# ---------------------------------------------------------------------------------------------------------------------------
def glyph(g, M, n, seed, w, f, strokes=3):
    """An angular rune of a few strokes on a 3 x 4 lattice, in the unit cell (-.5 .. .5)."""
    rng = np.random.default_rng(int(seed) & 0x7FFFFFFF)
    nodes = [(-.42 + .42 * i, -.5 + j / 3.0) for i in range(3) for j in range(4)]
    cur = int(rng.integers(0, 12))
    hw = w / 2
    for s in range(strokes):
        for _ in range(8):
            nxt = int(rng.integers(0, 12))
            dx, dy = nodes[nxt][0] - nodes[cur][0], nodes[nxt][1] - nodes[cur][1]
            if nxt != cur and abs(dy) > .2 or (abs(dx) > .3 and s > 0 and nxt != cur):
                break
        (x0, y0), (x1, y1) = nodes[cur], nodes[nxt]
        L = math.hypot(x1 - x0, y1 - y0)
        if L < 1e-6:
            continue
        px, py = -(y1 - y0) / L, (x1 - x0) / L
        # w is in metres; the cell's unit is the caller's (M scales x and y alike), so the half width is passed through M's scale
        k = hw / max(1e-6, math.dist(M(0, 0), M(0, 1)))
        g.face((M(x0 - px * k, y0 - py * k), M(x1 - px * k, y1 - py * k), M(x1 + px * k, y1 + py * k), M(x0 + px * k, y0 + py * k)), f, n)
        cur = nxt


def star(g, M, n, r1, r0, points, f, rot=0.0):
    """A star of `points` rays (tips at r1, valleys at r0) on the plane."""
    c = M(0, 0)
    for k in range(points):
        a = rot + 2 * math.pi * k / points
        h = math.pi / points
        tip = M(math.cos(a) * r1, math.sin(a) * r1)
        v0 = M(math.cos(a - h) * r0, math.sin(a - h) * r0)
        v1 = M(math.cos(a + h) * r0, math.sin(a + h) * r0)
        g.face((c, v0, tip, v1), f, n)


def floor_map(cx, y, cz):
    return lambda x, v: (cx + x, y, cz + v)


def floor_ring(g, cx, cz, r, w, y, f, n, a0=0.0, a1=2 * math.pi):
    for k in range(n):
        t0, t1 = a0 + (a1 - a0) * k / n, a0 + (a1 - a0) * (k + 1) / n
        g.face(((cx + math.cos(t0) * (r - w / 2), y, cz + math.sin(t0) * (r - w / 2)), (cx + math.cos(t1) * (r - w / 2), y, cz + math.sin(t1) * (r - w / 2)),
                (cx + math.cos(t1) * (r + w / 2), y, cz + math.sin(t1) * (r + w / 2)), (cx + math.cos(t0) * (r + w / 2), y, cz + math.sin(t0) * (r + w / 2))), f, Y)


def floor_rune_ring(g, cx, cz, r, h, y, f, T, seed):
    """Runes round a circle on a floor, their feet toward the centre."""
    if T.i == 2:
        floor_ring(g, cx, cz, r, h * .25, y, f, 16)
        return
    n = max(6, int(2 * math.pi * r / (h * 1.25)))
    for k in range(n):
        a = 2 * math.pi * k / n
        ca, sa = math.cos(a), math.sin(a)

        def M(x, v, ca=ca, sa=sa):
            rr = r + v * h
            return (cx + ca * rr - sa * x * h * .62, y, cz + sa * rr + ca * x * h * .62)
        glyph(g, M, Y, seed * 977 + k, h * .09, f, 3 if T.fine else 2)


def floor_strip(g, x0, x1, z0, z1, y, f):
    g.face(((x0, y, z0), (x1, y, z0), (x1, y, z1), (x0, y, z1)), f, Y)


# ---------------------------------------------------------------------------------------------------------------------------
# Surfaces
# ---------------------------------------------------------------------------------------------------------------------------
def grid_surface(g, P, f, closed_u=False, closed_v=False, inside=None, flat=False, fin_fn=None):
    """Quads over a grid of points P (m, n, 3) with smooth normals from the grid (flat: each quad two chiselled facets).
    The normals turn away from `inside` (default: the grid's centre). fin_fn(i, j) may give a quad its own finish."""
    P = np.asarray(P, dtype=np.float64)
    m, n, _ = P.shape

    def diff(A, axis, closed):
        if closed:
            return np.roll(A, -1, axis) - np.roll(A, 1, axis)
        G = np.empty_like(A)
        if axis == 0:
            G[1:-1] = A[2:] - A[:-2]
            G[0] = A[1] - A[0]
            G[-1] = A[-1] - A[-2]
        else:
            G[:, 1:-1] = A[:, 2:] - A[:, :-2]
            G[:, 0] = A[:, 1] - A[:, 0]
            G[:, -1] = A[:, -1] - A[:, -2]
        return G
    N = np.cross(diff(P, 0, closed_u), diff(P, 1, closed_v))
    c = P.reshape(-1, 3).mean(axis=0) if inside is None else np.asarray(inside, dtype=np.float64)
    out = P - c
    ln = np.linalg.norm(N, axis=2, keepdims=True)
    bad = ln[:, :, 0] < 1e-9
    N = N / np.maximum(ln, 1e-12)
    if (N * out).sum() < 0:
        N = -N
    if bad.any():
        o = out / np.maximum(np.linalg.norm(out, axis=2, keepdims=True), 1e-12)
        N[bad] = o[bad]
    for i in range(m if closed_u else m - 1):
        i1 = (i + 1) % m
        for j in range(n if closed_v else n - 1):
            j1 = (j + 1) % n
            ids = [(i, j), (i1, j), (i1, j1), (i, j1)]
            pts, nrm = [], []
            for a, b in ids:
                p = tuple(P[a, b])
                if pts and (math.dist(p, pts[-1]) < 1e-7):
                    continue
                pts.append(p)
                nrm.append(tuple(N[a, b]))
            if len(pts) > 1 and math.dist(pts[0], pts[-1]) < 1e-7:
                pts.pop()
                nrm.pop()
            if len(pts) < 3:
                continue
            ff = f if fin_fn is None else fin_fn(i, j)
            if flat:
                hint = tuple(N[i, j] + N[i1, j1])
                if len(pts) == 4:
                    facet2(g, pts[0], pts[1], pts[2], pts[3], ff, hint)
                else:
                    facet(g, pts, ff, hint)
            else:
                g.face(tuple(pts), ff, normals=tuple(nrm))


def limb(g, path, ra, rb, f, sides, ref=(1.0, 0.0, 0.0), mod=None, fin_fn=None, flat=False):
    """A surface lofted along a spine: at each point an ellipse (ra along `ref` made square to the spine, rb across), its
    radius scaled by mod(i, angle). Radii of 0 close the ends."""
    m = len(path)
    P = np.zeros((m, sides, 3))
    for i in range(m):
        a = path[max(0, i - 1)]
        b = path[min(m - 1, i + 1)]
        t = sk._norm(sk._sub(b, a))
        ua = sk._sub(ref, sk._lerp((0, 0, 0), t, sk._dot(ref, t)))
        ua = sk._norm(ua)
        ub = sk._cross(t, ua)
        for j in range(sides):
            ang = 2 * math.pi * j / sides
            k = 1.0 if mod is None else mod(i, ang)
            ca, sa = math.cos(ang) * ra[i] * k, math.sin(ang) * rb[i] * k
            P[i, j] = (path[i][0] + ua[0] * ca + ub[0] * sa, path[i][1] + ua[1] * ca + ub[1] * sa, path[i][2] + ua[2] * ca + ub[2] * sa)
    # the normals turn away from the spine: use the spine's middle as the inside for short limbs, per ring otherwise
    grid_surface(g, P, f, closed_v=True, inside=np.array(path).mean(axis=0), fin_fn=fin_fn, flat=flat)


def quat_mul(a, b):
    ax, ay, az, aw = a
    bx, by, bz, bw = b
    return (aw * bx + ax * bw + ay * bz - az * by, aw * by - ax * bz + ay * bw + az * bx, aw * bz + ax * by - ay * bx + az * bw, aw * bw - ax * bx - ay * by - az * bz)


def rock(g, c, r, seed, f, n=2, square=.35, rough=.3, flat=True, quat=(0, 0, 0, 1), freq=1.3):
    """A chiselled stone: a cube pulled toward a sphere (`square` 1 a block, 0 a ball), its skin pushed in and out by noise,
    radii r (x, y, z), turned by `quat`. flat: every facet its own normal; else smooth."""
    A = np.array(sk.affine(quat)).reshape(3, 4)[:, :3]
    faces = []
    lin = np.linspace(-1, 1, n + 1)
    for ax in range(3):
        for s in (-1, 1):
            G = np.zeros((n + 1, n + 1, 3))
            for i in range(n + 1):
                for j in range(n + 1):
                    p = [0.0, 0.0, 0.0]
                    p[ax] = s
                    p[(ax + 1) % 3] = lin[i]
                    p[(ax + 2) % 3] = lin[j]
                    G[i, j] = p
            faces.append(G)
    for G in faces:
        Q = G.reshape(-1, 3)
        d = Q / np.linalg.norm(Q, axis=1, keepdims=True)
        base = d * (1 - square) + Q * square * .82
        nz = sk.fbm(d * freq + seed * 7.13 + 40.0, 1.0, 2, int(seed) % 9973)
        base = base * (1 + rough * (nz - .5) * 2)[:, None]
        W = (base * np.array(r)) @ A.T + np.array(c)
        Wg = W.reshape(n + 1, n + 1, 3)
        if flat:
            for i in range(n):
                for j in range(n):
                    a, b, cc, dd = tuple(Wg[i, j]), tuple(Wg[i + 1, j]), tuple(Wg[i + 1, j + 1]), tuple(Wg[i, j + 1])
                    hint = tuple((Wg[i, j] + Wg[i + 1, j + 1]) / 2 - np.array(c))
                    facet2(g, a, b, cc, dd, f, hint)
        else:
            Nn = W - np.array(c)
            Nn = Nn / np.maximum(np.linalg.norm(Nn, axis=1, keepdims=True), 1e-9)
            Ng = Nn.reshape(n + 1, n + 1, 3)
            for i in range(n):
                for j in range(n):
                    ids = ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1))
                    g.face(tuple(tuple(Wg[a, b]) for a, b in ids), f, normals=tuple(tuple(Ng[a, b]) for a, b in ids))


def crystal(g, c, r, h, f, sides=6, quat=(0, 0, 0, 1), waist=.5, low=.45):
    """A faceted crystal: a `sides`-sided girdle at `waist` of its height, a long point up and a shorter one down (h up,
    h * low down)."""
    A = np.array(sk.affine(quat, c)).reshape(3, 4)

    def W(p):
        return tuple(A[:, :3] @ np.array(p) + A[:, 3])
    top, bot = W((0, h, 0)), W((0, -h * low, 0))
    ring = [W((math.cos(2 * math.pi * k / sides) * r, h * (waist - .5) * .6, math.sin(2 * math.pi * k / sides) * r)) for k in range(sides)]
    cc = W((0, 0, 0))
    for k in range(sides):
        a, b = ring[k], ring[(k + 1) % sides]
        mid = sk._lerp(a, b, .5)
        facet(g, (a, b, top), f, sk._sub(mid, cc))
        facet(g, (b, a, bot), f, sk._sub(mid, cc))


def prism(g, poly, height, f, T, cap=True, cell=None, top_poly=None):
    """A prism over a plan polygon [(x, y, z)] (or lofted to top_poly), its sides flat."""
    n = len(poly)
    cx = sum(p[0] for p in poly) / n
    cz = sum(p[2] for p in poly) / n
    tp = [(p[0], p[1] + height, p[2]) for p in poly] if top_poly is None else top_poly
    for i in range(n):
        a, b = poly[i], poly[(i + 1) % n]
        a1, b1 = tp[i], tp[(i + 1) % n]
        hint = ((a[0] + b[0]) / 2 - cx, 0.0, (a[2] + b[2]) / 2 - cz)
        rows = 1 if (T is None or T.i > 0) else max(1, int(height / 1.2))
        for r in range(rows):
            t0, t1 = r / rows, (r + 1) / rows
            facet(g, (sk._lerp(a, a1, t0), sk._lerp(b, b1, t0), sk._lerp(b, b1, t1), sk._lerp(a, a1, t1)), f, hint)
    if cap:
        g.face(list(tp), f, Y)


def octagon(cx, cz, h, c, y):
    return [(cx + h, y, cz - h + c), (cx + h, y, cz + h - c), (cx + h - c, y, cz + h), (cx - h + c, y, cz + h),
            (cx - h, y, cz + h - c), (cx - h, y, cz - h + c), (cx - h + c, y, cz - h), (cx + h - c, y, cz - h)]


def rect_plan(x0, x1, z0, z1, y):
    return [(x0, y, z0), (x1, y, z0), (x1, y, z1), (x0, y, z1)]


def mould_round(g, poly, profile, f, T):
    """A moulding swept round a closed plan polygon: profile [(out, up)]."""
    path = list(poly)
    n = len(path)
    area = 0.0
    for i in range(n):
        a, b = path[i], path[(i + 1) % n]
        area += a[0] * b[2] - b[0] * a[2]
    if area < 0:
        path = path[::-1]
    if T is not None and T.i == 2:
        o0, o1 = min(p[0] for p in profile), max(p[0] for p in profile)
        u0, u1 = min(p[1] for p in profile), max(p[1] for p in profile)
        profile = [(o0, u0), (o1 * .85, u0), (o1 * .85, u1), (o0, u1)]
    sweep(g, profile, path, f, closed=True, path_normals=[Y] * n, smooth_u=False)


def bracket(g, fc, a, w, ytop, reach, drop, lip, f, T, d0=0.0):
    """A curved corbel on a face: from the wall `drop` below ytop out to `reach` under the slab it carries (a cavetto)."""
    n = max(3, T.arc // 4)
    pts = []
    for i in range(n + 1):
        th = math.pi / 2 * i / n
        pts.append((d0 + reach - reach * math.sin(th), ytop - drop + (drop - lip) * math.cos(th)))
    for s, aa in ((-1, a - w / 2), (1, a + w / 2)):
        nr = sk._lerp((0, 0, 0), fc.u, s)
        for i in range(n):
            (d1, y1), (d2, y2) = pts[i], pts[i + 1]
            g.face((fc.P(aa, y1, d1), fc.P(aa, y2, d2), fc.P(aa, ytop, d2), fc.P(aa, ytop, d1)), f, normal=nr)
    for i in range(n):
        (d1, y1), (d2, y2) = pts[i], pts[i + 1]

        def nrm(k):
            th = math.pi / 2 * k / n
            # the curve's outward normal: down and away from the wall's top corner
            return sk._norm(sk._add(sk._lerp((0, 0, 0), fc.n, (drop - lip) * math.sin(th)), Y, -reach * math.cos(th) - 1e-6))
        g.face((fc.P(a - w / 2, y1, d1), fc.P(a + w / 2, y1, d1), fc.P(a + w / 2, y2, d2), fc.P(a - w / 2, y2, d2)), f, normals=(nrm(i), nrm(i), nrm(i + 1), nrm(i + 1)))
    g.face((fc.P(a - w / 2, ytop - lip, d0 + reach), fc.P(a + w / 2, ytop - lip, d0 + reach), fc.P(a + w / 2, ytop, d0 + reach), fc.P(a - w / 2, ytop, d0 + reach)), f, fc.n)


def toothed(g, b, f, T, lo, hi, seed, course=.3, jag=.55, bevel=.02, skip=(), cell=None):
    """A wall or rail along its long axis whose ends may be broken: courses of stones standing out by their own lengths."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    along_x = (x1 - x0) >= (z1 - z0)
    a0, a1 = (x0, x1) if along_x else (z0, z1)
    j0 = min(jag, (a1 - a0) * .3) if lo else 0.0
    j1 = min(jag, (a1 - a0) * .3) if hi else 0.0

    def B(u0, u1, v0, v1, bev, cell, sk_=()):
        if along_x:
            box(g, u0, u1, v0, v1, z0, z1, f, T, bevel=bev, cell=cell, skip=sk_)
        else:
            box(g, x0, x1, v0, v1, u0, u1, f, T, bevel=bev, cell=cell, skip=sk_)
    B(a0 + j0, a1 - j1, y0, y1, bevel, cell, skip)
    n = max(1, int(round((y1 - y0) / course)))
    if T.i == 2:
        n = max(1, n // 2)
    for end, j in ((0, j0), (1, j1)):
        if j <= 0:
            continue
        for k in range(n):
            v0, v1 = y0 + (y1 - y0) * k / n, y0 + (y1 - y0) * (k + 1) / n
            s = j * min(1.0, (.15 + .85 * ((k + .5) / n) ** .8) * (.55 + .6 * hashf(seed, end, k)))
            if j - s < .02:
                continue
            bv = .03 if T.fine else 0.0
            if end == 0:
                B(a0 + s, a0 + j0 + .01, v0, v1, bv, 0)
            else:
                B(a1 - j1 - .01, a1 - s, v0, v1, bv, 0)


def debris(g, x, y, z, spread, count, seed, f=None, size=.16):
    """Small broken stones lying on a floor round a point (none taller than a hand: nothing to hide behind)."""
    ff = F['rubble'] if f is None else f
    for i in range(count):
        a = hashf(seed, i, 1) * 2 * math.pi
        d = spread * (.2 + .8 * hashf(seed, i, 2))
        s = size * (.5 + hashf(seed, i, 3))
        q = sk.quat_axis(hashf(seed, i, 4) - .5, 1, hashf(seed, i, 5) - .5, hashf(seed, i, 6) * 6.28)
        rock(g, (x + math.cos(a) * d, y + s * .42, z + math.sin(a) * d), (s, s * .5, s * .75), seed * 31 + i, ff, n=1, square=.6, rough=.25, quat=q)
