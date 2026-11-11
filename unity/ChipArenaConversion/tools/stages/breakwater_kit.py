"""Breakwater's modelling helpers (breakwater_art.py, breakwater_props.py and breakwater_far.py build on them; stagekit.py is the
shared kit): its finishes, dressed-granite pieces (coping stones laid one by one, bullnoses, quarried blocks, armour stones),
ironwork (rings, ladders, straps, lathed bollards and lamp posts), timber (fender piles, planks, lofted boat hulls) and water
(wave sections swept along a crest). Everything is in the game's space (Unity: metres, +y up)."""
import math

import numpy as np

import stagekit as sk
from stagekit import Geo, box, sweep, lathe, grid_face

Y = (0.0, 1.0, 0.0)
F = {}


def finishes():
    A = sk
    # the stone: calm flagstone joints on the decks and big ashlar on the walls (the rougher fine surfaces break into blotches
    # under the anime style's stepped light); wet, so the lamp's beam and the lanterns catch on it
    F['sett'] = A.fin('bw-sett', 0x586070, A.DETAIL_FLAG, smooth=.52, param=.9, dirt=.55)
    F['walk'] = A.fin('bw-walk', 0x60687A, A.DETAIL_FLAG, smooth=.5, param=1.2, dirt=.55)
    F['quay'] = A.fin('bw-quay', 0x4A5260, A.DETAIL_FLAG, smooth=.58, param=1.1, dirt=.6, moss=.25)
    F['granite'] = A.fin('bw-granite', 0x646C7C, A.DETAIL_FLAG, smooth=.34, param=1.9, dirt=.7, moss=.9)
    F['ashlar'] = A.fin('bw-ashlar', 0x6E7688, A.DETAIL_FLAG, smooth=.3, param=1.5, dirt=.6, moss=.3)
    F['coping'] = A.fin('bw-coping', 0x808898, A.DETAIL_NONE, smooth=.42, wear=.35, dirt=.5, moss=.2)
    F['dressed'] = A.fin('bw-dressed', 0x767E8E, A.DETAIL_NONE, smooth=.36, wear=.3, dirt=.55, moss=.2)
    F['rough'] = A.fin('bw-rough', 0x727A8A, A.DETAIL_NONE, smooth=.2, wear=.2, dirt=.7)
    F['deep'] = A.fin('bw-deep', 0x3C4149, A.DETAIL_NONE, smooth=.2, dirt=.4)
    F['armour'] = A.fin('bw-armour', 0x4C525A, A.DETAIL_NONE, smooth=.4, dirt=.5, moss=1.0)
    F['armourwet'] = A.fin('bw-armourwet', 0x30363E, A.DETAIL_NONE, smooth=.65, dirt=.3, moss=1.0)
    F['puddle'] = A.fin('bw-puddle', 0x262C36, A.DETAIL_NONE, smooth=.95, dirt=0)
    F['lime'] = A.fin('bw-lime', 0xD4D6D6, A.DETAIL_PLASTER, smooth=.22, dirt=.6)
    F['band'] = A.fin('bw-band', 0xA83A30, A.DETAIL_PLASTER, smooth=.25, dirt=.6)
    F['slate'] = A.fin('bw-slate', 0x3A424E, A.DETAIL_NONE, smooth=.5, dirt=.4, wear=.2)
    F['lead'] = A.fin('bw-lead', 0x6A727C, A.DETAIL_NONE, smooth=.5, metal=.2, dirt=.4)
    # metals keep most of their own colour (the probe's sky is dark)
    F['iron'] = A.fin('bw-iron', 0x262A30, A.DETAIL_IRON, smooth=.5, metal=.4, dirt=.4, wear=.5)
    F['ironplain'] = A.fin('bw-ironplain', 0x262A30, A.DETAIL_NONE, smooth=.5, metal=.4, dirt=.3, wear=.5)
    F['rust'] = A.fin('bw-rust', 0x70462E, A.DETAIL_NONE, smooth=.25, metal=.2, dirt=.6)
    F['brass'] = A.fin('bw-brass', 0xB89A52, A.DETAIL_NONE, smooth=.7, metal=.5, dirt=.3, wear=.3)
    F['bronze'] = A.fin('bw-bronze', 0x7A6A40, A.DETAIL_NONE, smooth=.55, metal=.5, dirt=.5, wear=.3)
    F['copper'] = A.fin('bw-copper', 0x4F8A72, A.DETAIL_NONE, smooth=.45, metal=.35, dirt=.5)
    F['timber'] = A.fin('bw-timber', 0x5A4430, A.DETAIL_NONE, smooth=.25, dirt=.7, moss=.4, wear=.2)
    F['plank'] = A.fin('bw-plank', 0x7A6444, A.DETAIL_NONE, smooth=.22, dirt=.6, wear=.2)
    F['tar'] = A.fin('bw-tar', 0x2A2420, A.DETAIL_NONE, smooth=.35, dirt=.3)
    F['hull'] = A.fin('bw-hull', 0x2E4A5A, A.DETAIL_NONE, smooth=.4, dirt=.5, wear=.3)
    F['hullwhite'] = A.fin('bw-hullwhite', 0xD8D4C8, A.DETAIL_NONE, smooth=.4, dirt=.5, wear=.2)
    F['hullred'] = A.fin('bw-hullred', 0x8A3028, A.DETAIL_NONE, smooth=.35, dirt=.6)
    F['rope'] = A.fin('bw-rope', 0xB09A6C, A.DETAIL_NONE, smooth=.15, dirt=.6)
    F['net'] = A.fin('bw-net', 0x4A6A5A, A.DETAIL_NONE, smooth=.15, dirt=.6)
    F['wicker'] = A.fin('bw-wicker', 0x8A7048, A.DETAIL_NONE, smooth=.2, dirt=.6, wear=.2)
    F['glass'] = A.fin('bw-glass', 0x1C2A3A, A.DETAIL_NONE, smooth=.9, dirt=.1)
    F['door'] = A.fin('bw-door', 0x2C4440, A.DETAIL_NONE, smooth=.35, dirt=.5, wear=.3)
    F['red'] = A.fin('bw-red', 0xB8322A, A.DETAIL_NONE, smooth=.45, dirt=.5, wear=.4)
    F['cloth'] = A.fin('bw-cloth', 0xB8322A, A.DETAIL_NONE, smooth=.2, dirt=.3)
    F['clothwhite'] = A.fin('bw-clothwhite', 0xE8E4DA, A.DETAIL_NONE, smooth=.2, dirt=.3)
    F['fish'] = A.fin('bw-fish', 0x9AB0BE, A.DETAIL_NONE, smooth=.8, dirt=.1)
    # the sea
    F['sea'] = A.fin('bw-sea', 0x15324A, A.DETAIL_NONE, smooth=.78, dirt=0)
    F['sealight'] = A.fin('bw-sealight', 0x1E4560, A.DETAIL_NONE, smooth=.72, dirt=0)
    F['foam'] = A.fin('bw-foam', 0xDCE8EE, A.DETAIL_NONE, smooth=.3, dirt=0)
    F['foamlit'] = A.fin('bw-foamlit', 0xCFE2EC, glow=.42)
    # birds
    F['gull'] = A.fin('bw-gull', 0xECEEF0, A.DETAIL_NONE, smooth=.3, dirt=0)
    F['gullgrey'] = A.fin('bw-gullgrey', 0x98A2AE, A.DETAIL_NONE, smooth=.3, dirt=0)
    F['gulldark'] = A.fin('bw-gulldark', 0x2A2E36, A.DETAIL_NONE, smooth=.3, dirt=0)
    F['beak'] = A.fin('bw-beak', 0xE0A030, A.DETAIL_NONE, smooth=.4, dirt=0)
    # the far shore
    F['house0'] = A.fin('bw-house0', 0x4A4E5C, A.DETAIL_NONE, smooth=.2, dirt=.3)
    F['house1'] = A.fin('bw-house1', 0x5C5458, A.DETAIL_NONE, smooth=.2, dirt=.3)
    F['house2'] = A.fin('bw-house2', 0x6A6468, A.DETAIL_NONE, smooth=.2, dirt=.3)
    F['roof'] = A.fin('bw-roof', 0x2C3240, A.DETAIL_NONE, smooth=.3, dirt=.2)
    F['hill'] = A.fin('bw-hill', 0x1C2634, A.DETAIL_NONE, smooth=.1, dirt=0)
    F['hillfar'] = A.fin('bw-hillfar', 0x263248, A.DETAIL_NONE, smooth=.1, dirt=0)
    # the lights (the glow sub-mesh; gain above 1 blooms)
    F['lamp'] = A.fin('bw-lamp', 0xFFC878, glow=2.6)
    F['lampsoft'] = A.fin('bw-lampsoft', 0xFFC27A, glow=1.3)
    F['window'] = A.fin('bw-window', 0xFFD79A, glow=1.7)
    F['windowfar'] = A.fin('bw-windowfar', 0xFFC27A, glow=2.4)
    F['lens'] = A.fin('bw-lens', 0xFFF0C8, glow=3.4)
    F['white'] = A.fin('bw-white', 0xFFFFFF, glow=1.0)
    F['redlamp'] = A.fin('bw-redlamp', 0xFF4A38, glow=2.8)
    F['greenlamp'] = A.fin('bw-greenlamp', 0x5AFFA0, glow=2.4)


def ext(b):
    p, s = b['pos'], b['size']
    return (p[0] - s[0] / 2, p[0] + s[0] / 2, p[1] - s[1] / 2, p[1] + s[1] / 2, p[2] - s[2] / 2, p[2] + s[2] / 2)


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


def rock(g, c, r, seed, f, n=2, square=.35, rough=.3, flat=True, quat=(0, 0, 0, 1), freq=1.3):
    """A chiselled stone: a cube pulled toward a sphere (`square` 1 a block, 0 a ball), its skin pushed in and out by noise,
    radii r (x, y, z), turned by `quat`. flat: every facet its own normal; else smooth."""
    A = np.array(sk.affine(quat)).reshape(3, 4)[:, :3]
    lin = np.linspace(-1, 1, n + 1)
    cc = np.array(c, dtype=np.float64)
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
            Q = G.reshape(-1, 3)
            d = Q / np.linalg.norm(Q, axis=1, keepdims=True)
            base = d * (1 - square) + Q * square * .82
            nz = sk.fbm(d * freq + seed * 7.13 + 40.0, 1.0, 2, int(seed) % 9973)
            base = base * (1 + rough * (nz - .5) * 2)[:, None]
            W = (base * np.array(r)) @ A.T + cc
            Wg = W.reshape(n + 1, n + 1, 3)
            Nn = W - cc
            Nn = Nn / np.maximum(np.linalg.norm(Nn, axis=1, keepdims=True), 1e-9)
            Ng = Nn.reshape(n + 1, n + 1, 3)
            for i in range(n):
                for j in range(n):
                    ids = ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1))
                    if flat:
                        a, b, c2, d2 = (tuple(Wg[p, q]) for p, q in ids)
                        hint = tuple((Wg[i, j] + Wg[i + 1, j + 1]) / 2 - cc)
                        facet(g, (a, b, c2), f, hint)
                        facet(g, (a, c2, d2), f, hint)
                    else:
                        g.face(tuple(tuple(Wg[p, q]) for p, q in ids), f, normals=tuple(tuple(Ng[p, q]) for p, q in ids))


def grid_surface(g, P, f, closed_u=False, closed_v=False, inside=None, fin_fn=None, flip=False):
    """Quads over a grid of points P (m, n, 3) with smooth normals from the grid, turned away from `inside` (default: the
    grid's centre; flip: toward it). fin_fn(i, j) may give a quad its own finish."""
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
    if ((N * out).sum() < 0) != flip:
        N = -N
    if bad.any():
        o = out / np.maximum(np.linalg.norm(out, axis=2, keepdims=True), 1e-12)
        N[bad] = -o[bad] if flip else o[bad]
    for i in range(m if closed_u else m - 1):
        i1 = (i + 1) % m
        for j in range(n if closed_v else n - 1):
            j1 = (j + 1) % n
            pts, nrm = [], []
            for a, b in ((i, j), (i1, j), (i1, j1), (i, j1)):
                p = tuple(P[a, b])
                if pts and math.dist(p, pts[-1]) < 1e-7:
                    continue
                pts.append(p)
                nrm.append(tuple(N[a, b]))
            if len(pts) > 1 and math.dist(pts[0], pts[-1]) < 1e-7:
                pts.pop()
                nrm.pop()
            if len(pts) < 3:
                continue
            g.face(tuple(pts), f if fin_fn is None else fin_fn(i, j), normals=tuple(nrm))


class Face:
    """A wall face's frame: a along it (u = Y x n), y the world's, d out of it."""

    def __init__(self, o, n, length):
        self.o, self.n, self.L = o, n, length
        self.u = (n[2], 0.0, -n[0])

    def P(self, a, y, d=0.0):
        o, u, n = self.o, self.u, self.n
        return (o[0] + u[0] * a + n[0] * d, y, o[2] + u[2] * a + n[2] * d)

    def box(self, g, a0, a1, y0, y1, d0, d1, f, T, bevel=.02, cell=0, skip=()):
        p, q = self.P(a0, y0, d0), self.P(a1, y1, d1)
        box(g, min(p[0], q[0]), max(p[0], q[0]), y0, y1, min(p[2], q[2]), max(p[2], q[2]), f, T, bevel if (T is None or T.i == 0) else 0, cell, skip)

    def quad(self, g, a0, a1, y0, y1, d, f):
        g.face((self.P(a0, y0, d), self.P(a1, y0, d), self.P(a1, y1, d), self.P(a0, y1, d)), f, self.n)

    def mould(self, g, profile, y, a0, a1, f, T, caps=True):
        """A moulding along the face: profile [(up, out)] from the path at height y."""
        if T.i == 2:
            u0, u1 = min(p[0] for p in profile), max(p[0] for p in profile)
            o = max(p[1] for p in profile) * .8
            profile = [(u0, -.01), (u0, o), (u1, o), (u1, -.01)]
        sweep(g, profile, [self.P(a0, y, 0), self.P(a1, y, 0)], f, path_normals=[self.n, self.n], smooth_u=False, caps=caps)

    def arch(self, g, ac, half, y0, ys, rise, d, f_field, f_trim, T, trim=.14, proud=.07):
        """A blind round-headed arch: its field (flat, at depth d over the face) and a raised band round it."""
        n = (10, 6, 3)[T.i]
        pts = [(ac - half, y0), (ac - half, ys)] + [(ac - half * math.cos(math.pi * k / n), ys + rise * math.sin(math.pi * k / n)) for k in range(1, n)] + [(ac + half, ys), (ac + half, y0)]
        # the field: one convex face
        g.face([self.P(a, y, d) for a, y in pts], f_field, self.n)
        if T.i == 2:
            return
        # the band: outside the opening by `trim`, standing `proud`
        outer = [(ac - half - trim, y0), (ac - half - trim, ys)] + [(ac - (half + trim) * math.cos(math.pi * k / n), ys + (rise + trim) * math.sin(math.pi * k / n)) for k in range(1, n)] + [(ac + half + trim, ys), (ac + half + trim, y0)]
        for k in range(len(pts) - 1):
            a, b, c, e = pts[k], pts[k + 1], outer[k + 1], outer[k]
            g.face((self.P(*a, proud), self.P(*b, proud), self.P(*c, proud), self.P(*e, proud)), f_trim, self.n)
            # its inner and outer cheeks
            mid = ((a[0] + b[0]) / 2 - ac, (a[1] + b[1]) / 2 - ys)
            l = math.hypot(*mid) or 1.0
            ni = sk._add(sk._lerp((0, 0, 0), self.u, -mid[0] / l), Y, -max(0.0, mid[1]) / l - 1e-6)
            g.face((self.P(*a, d), self.P(*b, d), self.P(*b, proud), self.P(*a, proud)), f_trim, normal=ni)
            no = (-ni[0], -ni[1], -ni[2])
            g.face((self.P(*e, 0), self.P(*c, 0), self.P(*c, proud), self.P(*e, proud)), f_trim, normal=no)


def faces_of(e):
    """The four wall faces of an extent: name -> Face (a runs so that the face is seen left to right from outside)."""
    x0, x1, y0, y1, z0, z1 = e
    return {'+z': Face((x0, 0, z1), (0, 0, 1), x1 - x0), '-z': Face((x1, 0, z0), (0, 0, -1), x1 - x0),
            '+x': Face((x1, 0, z1), (1, 0, 0), z1 - z0), '-x': Face((x0, 0, z0), (-1, 0, 0), z1 - z0)}


# ---------------------------------------------------------------------------------------------------------------------------
# Dressed stone
# ---------------------------------------------------------------------------------------------------------------------------
def coping_run(g, fc, a0, a1, y, depth, T, seed, over=.07, h=.2, f=None, stone=1.25, sunk=.02, missing=0.0):
    """Coping stones laid one by one along a face's top edge: each `depth` deep from the face inward and `over` proud of it,
    `h` thick with its top `sunk` under y + h's nominal line by its own hair, bullnosed; a share `missing` knocked out."""
    ff = F['coping'] if f is None else f
    n = max(1, int(round((a1 - a0) / stone)))
    if T.i == 2:
        fc.box(g, a0, a1, y - .02, y + h - .02, -depth, over, ff, T, bevel=0)
        return
    step = (a1 - a0) / n
    for i in range(n):
        if hashf(seed, i, 7) < missing:
            continue
        j = .012 if T.i == 0 else .02
        dy = (hashf(seed, i, 1) - .5) * .02
        do = (hashf(seed, i, 2) - .5) * .02
        fc.box(g, a0 + step * i + j, a0 + step * (i + 1) - j, y - .02, y + h - .02 + dy, -depth, over + do, ff, None, bevel=.045 if T.i == 0 else .03)


def courses(g, fc, a0, a1, y0, y1, T, seed, d=.012, course=.62, f=None):
    """Raised ashlar on a face: stones standing a hair proud in courses with open joints (Fine: stone by stone; Mid: course by
    course; Low: nothing, the face's own finish carries it)."""
    if T.i == 2:
        return
    ff = F['dressed'] if f is None else f
    rows = max(1, int(round((y1 - y0) / course)))
    rh = (y1 - y0) / rows
    for r in range(rows):
        if T.i == 1:
            fc.quad(g, a0, a1, y0 + rh * r + .02, y0 + rh * (r + 1) - .02, d, ff)
            continue
        a = a0
        k = 0
        while a < a1 - .05:
            w = min(a1 - a, (.9 + hashf(seed, r, k) * .9) if not (k == 0 and r % 2) else .55)
            if a1 - (a + w) < .3:
                w = a1 - a
            fc.quad(g, a + .014, a + w - .014, y0 + rh * r + .014, y0 + rh * (r + 1) - .014, d + hashf(seed, r, k, 3) * .01, ff)
            a += w
            k += 1


def ring(g, c, n, r, t, f, T, hang=.0):
    """An iron mooring ring on a face: centre c, the face's normal n (it hangs flat against the face, dropped by `hang`)."""
    seg = (12, 8, 5)[T.i]
    u = sk._norm(sk._cross(Y, n))
    path = [sk._add(sk._add(sk._add(c, u, math.cos(2 * math.pi * k / seg) * r), Y, math.sin(2 * math.pi * k / seg) * r - hang), n, t * 1.2) for k in range(seg)]
    sk.tube(g, path, t, f, (5, 4, 3)[T.i], closed=True)
    if T.i < 2:
        box(g, c[0] - .05, c[0] + .05, c[1] + r - hang - .05, c[1] + r - hang + .05, c[2] - .05, c[2] + .05, f, None, bevel=.01)


def ladder(g, fc, a, y0, y1, T, d=.12):
    """An iron ladder down a face."""
    s = (5, 4, 3)[T.i]
    for o in (-.2, .2):
        sk.cylinder(g, fc.P(a + o, y0, d), fc.P(a + o, y1 + .5, d), .022, F['ironplain'], s)
    if T.i == 2:
        return
    y = y0 + .2
    while y < y1 + .3:
        sk.cylinder(g, fc.P(a - .2, y, d), fc.P(a + .2, y, d), .016, F['ironplain'], s, caps=False)
        y += .32


def fender(g, fc, a, y0, y1, T, w=.24, d=.2):
    """A timber fender pile on a quay's face, iron-strapped."""
    fc.box(g, a - w / 2, a + w / 2, y0, y1, 0, d, F['timber'], T, bevel=.03)
    if T.i < 2:
        for y in (y1 - .35, (y0 + y1) / 2 + .4):
            fc.box(g, a - w / 2 - .015, a + w / 2 + .015, y - .04, y + .04, 0, d + .015, F['ironplain'], T, bevel=.008)


def quarried(g, e, T, seed, f=None):
    """A stack of quarried granite blocks filling an extent: rough-hewn stones in courses, their joints staggered, each a
    little out of true; drill marks down the split faces; chocked on timber skids, a lifting chain left over the top."""
    x0, x1, y0, y1, z0, z1 = e
    ff = F['rough'] if f is None else f
    rows = max(1, int(round((y1 - y0) / .9)))
    rh = (y1 - y0) / rows
    long_x = (x1 - x0) >= (z1 - z0)
    a0, a1 = (x0, x1) if long_x else (z0, z1)
    c0, c1 = (z0, z1) if long_x else (x0, x1)
    for r in range(rows):
        cuts = [a0, a0 + (a1 - a0) * (.5 if (a1 - a0) < 1.7 else (.42 if r % 2 == 0 else .6)), a1] if (a1 - a0) > 1.5 and T.i < 2 else [a0, a1]
        yb = y0 + rh * r + (.06 if r == 0 else .012)
        yt = y0 + rh * (r + 1) - (.0 if r == rows - 1 else .012)
        for c in range(len(cuts) - 1):
            ua, ub = cuts[c] + (.012 if c else 0), cuts[c + 1] - (.012 if c < len(cuts) - 2 else 0)
            ins = [hashf(seed, r, c, k) * .06 for k in range(2)]
            va, vb = c0 + ins[0], c1 - ins[1]
            cx, cz = ((ua + ub) / 2, (va + vb) / 2) if long_x else ((va + vb) / 2, (ua + ub) / 2)
            hx, hz = ((ub - ua) / 2, (vb - va) / 2) if long_x else ((vb - va) / 2, (ub - ua) / 2)
            if T.i == 2:
                box(g, cx - hx, cx + hx, yb, yt, cz - hz, cz + hz, ff, None, bevel=0)
                continue
            k = .80
            rock(g, (cx, (yb + yt) / 2, cz), (hx / k, (yt - yb) / 2 / k, hz / k), seed * 13 + r * 5 + c, ff, n=(4, 2)[T.i], square=.88, rough=.07, freq=3.1)
            if T.i == 0:
                # drill marks down a split face
                for q in range(4):
                    u = ua + (ub - ua) * (.2 + .2 * q)
                    px, pz = (u, c0 + ins[0] + .012) if long_x else (c0 + ins[0] + .012, u)
                    box(g, px - .011, px + .011, yb + .16, yb + .16 + (yt - yb) * .45, pz - .02, pz + .02, F['deep'], None, bevel=0)
    if T.i < 2:
        for t in (.25, .75):
            u = a0 + (a1 - a0) * t
            if long_x:
                box(g, u - .08, u + .08, y0, y0 + .07, z0 + .03, z1 - .03, F['timber'], None, bevel=.01)
            else:
                box(g, x0 + .03, x1 - .03, y0, y0 + .07, u - .08, u + .08, F['timber'], None, bevel=.01)
        # the chain: over the top and down one side
        cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
        n = 9
        path = [(cx - (x1 - x0) * .2, y1 + .03, z0 + (z1 - z0) * k / n * .96 + .02) for k in range(n + 1)] + [(cx - (x1 - x0) * .2, y1 - .1 - .22 * k, z1 - .0 + .02) for k in range(4)]
        sk.tube(g, path, .028, F['rust'], 4)
        ring(g, (cx + (x1 - x0) * .18, y1 + .0, cz), (0, 1, 0), .12, .022, F['rust'], T) if False else None


def bollard(g, cx, y, cz, T, r=.3, h=.9):
    """A cast-iron mooring bollard: a flared foot, a waisted shaft, a mushroom head."""
    prof = [(r * 1.0, 0), (r * 1.0, .06), (r * .78, .1), (r * .62, .2), (r * .58, h * .62), (r * .7, h * .72), (r * 1.0, h * .8), (r * 1.05, h * .9), (r * .9, h * .97), (r * .4, h)]
    if T.i == 2:
        prof = [prof[0], prof[3], prof[5], prof[7], prof[9]]
    lathe(g, prof, (cx, y, cz), F['ironplain'], (14, 9, 6)[T.i], cap1=True)


def rope_coil(g, cx, y, cz, r, T, turns=3):
    """A coil of rope lying on a floor."""
    if T.i == 2:
        return
    seg = (16, 9)[T.i]
    for k in range(turns):
        rr = r - k * .055
        path = [(cx + math.cos(2 * math.pi * i / seg) * rr, y + .035 + k * .045, cz + math.sin(2 * math.pi * i / seg) * rr) for i in range(seg)]
        sk.tube(g, path, .032, F['rope'], (5, 4)[T.i], closed=True)


def lamp_post(g, cx, y0, y1, cz, tip, T):
    """A cast-iron lamp post from y0 to y1 and its scrolled bracket out to `tip` (where the lantern's ring hangs)."""
    s = (10, 7, 5)[T.i]
    prof = [(.2, 0), (.2, .1), (.15, .16), (.13, .34), (.085, .42), (.075, 1.5), (.1, 1.55), (.1, 1.62), (.07, 1.68), (.06, y1 - y0 - .2), (.085, y1 - y0 - .14), (.085, y1 - y0 - .06), (.04, y1 - y0)]
    if T.i == 2:
        prof = [(.18, 0), (.08, .4), (.06, y1 - y0 - .1), (.04, y1 - y0)]
    lathe(g, prof, (cx, y0, cz), F['iron'], s, cap1=True)
    top = (cx, y1 - .12, cz)
    # the arm: a rising swan-neck to the tip, a scroll under it
    n = (8, 5, 2)[T.i]
    arm = [sk._add(sk._lerp(top, (tip[0], tip[1] + .06, tip[2]), k / n), Y, .14 * math.sin(math.pi * k / n)) for k in range(n + 1)]
    sk.tube(g, arm, .028, F['ironplain'], (6, 5, 4)[T.i])
    if T.i < 2:
        d = sk._norm(sk._sub((tip[0], 0, tip[2]), (cx, 0, cz)))
        m = (7, 5)[T.i]
        scroll = [sk._add(sk._add((cx, y1 - .55, cz), d, .3 * (1 - math.cos(math.pi * .5 * k / m)) + .07), Y, .38 * math.sin(math.pi * .5 * k / m)) for k in range(m + 1)]
        sk.tube(g, scroll, .018, F['ironplain'], 4)
        sk.cylinder(g, (tip[0], tip[1] + .06, tip[2]), (tip[0], tip[1] - .02, tip[2]), .012, F['ironplain'], 4)
        # the ladder bar
        sk.cylinder(g, sk._add((cx, y0 + 2.45, cz), sk._cross(Y, d), -.3), sk._add((cx, y0 + 2.45, cz), sk._cross(Y, d), .3), .016, F['ironplain'], 4)


def hull(g, length, beam, depth, T, f_side, f_bottom=None, sheer=.18, stern=.75, f_in=None, wale=None, y0=0.0, flip=False):
    """A boat's hull lofted along z (bow at +z), its keel at y0 and its gunwale at y0 + depth (+ sheer toward the ends): the
    outer skin, an inner skin and the gunwale's rail. flip: keel up (an upturned boat)."""
    m = (14, 9, 6)[T.i]
    n = (9, 7, 5)[T.i]
    P = np.zeros((m + 1, 2 * n + 1, 3))
    for i in range(m + 1):
        t = i / m                                   # 0 stern .. 1 bow
        z = (t - .5) * length
        # half breadth along the hull: full amidships, fine at the bow, a transom's share at the stern
        w = beam / 2 * (math.sin(math.pi * min(1.0, (1 - t) * 1.25 + .0)) ** .55 if t > .5 else (stern + (1 - stern) * math.sin(math.pi * t)))
        w = max(w, .02)
        top = depth + sheer * (2 * t - 1) ** 2
        for j in range(2 * n + 1):
            a = (j / (2 * n) - .5) * math.pi        # -90 (port gunwale) .. +90
            x = math.sin(a) * w
            y = top * (1 - math.cos(a) ** .7)
            P[i, j] = (x, y0 + (depth + sheer - y if flip else y), z)
    below = lambda i, j: (f_bottom if (f_bottom is not None and abs(j - n + .5) < n * .55) else f_side)
    grid_surface(g, P, f_side, inside=(0, y0 + (0 if flip else depth * 2), 0), fin_fn=below)
    if not flip:
        Q = P.copy()
        Q[:, :, 0] *= .9
        Q[:, :, 1] = Q[:, :, 1] * .9 + (y0 + depth) * .1 + .03
        grid_surface(g, Q, f_in or f_side, inside=(0, y0 + depth * 2, 0), flip=True)
        # the transom and the gunwale rail
        g.face([tuple(P[0, j]) for j in range(2 * n + 1)], f_side, normal=(0, 0, -1))
    for side in (0, 2 * n):
        path = [tuple(P[i, side]) for i in range(m + 1)]
        sk.tube(g, path, .035 if T.i < 2 else .05, wale or F['tar'], (5, 4, 3)[T.i])
    return P


def wave(g, half, height, depth, T, f, f_foam, curl=.0, foam=.22, ends=.35):
    """A wave's body along x (-half..half), its crest at `height`, `depth` deep each way from the crest line (z 0): the
    section a steep face toward -z (the way it runs) and a long back toward +z, the ends tapering down; a band of foam along
    the crest."""
    m = (18, 10, 6)[T.i]
    n = (9, 6, 4)[T.i]
    P = np.zeros((m + 1, n + 1, 3))
    for i in range(m + 1):
        u = i / m * 2 - 1
        k = 1.0 if abs(u) < 1 - ends else .5 + .5 * math.cos(math.pi * (abs(u) - (1 - ends)) / ends)
        for j in range(n + 1):
            v = j / n                               # 0 front foot .. 1 back foot
            z = (-depth + 2 * depth * v)
            # the crest sits a third of the way from the front; the front face steeper
            c = .36
            hgt = math.sin(math.pi / 2 * v / c) ** 1.5 if v < c else math.cos(math.pi / 2 * (v - c) / (1 - c)) ** 1.25
            zc = z - curl * depth * hgt * (1 if v < c else .4)
            P[i, j] = (u * half, hgt * height * k * (1 + .08 * math.sin(u * 9 + v * 3)), zc * (.55 + .45 * k))
    c_j = max(1, round(n * .36))
    grid_surface(g, P, f, inside=(0, -height * 4, 0), fin_fn=lambda i, j: f_foam if (foam > 0 and abs(j + .5 - c_j) < max(.9, n * foam / 2) and 0 < i < m - 1) else f)
    return P


def lifebuoy(g, fc, a, y, T, r=.3, t=.07):
    """A lifebuoy hung on a face: red and white quarters, a grab line round it, the peg it hangs on."""
    seg = (16, 8, 8)[T.i]
    sides = (6, 4, 3)[T.i]
    for q in range(4):
        n = seg // 4
        path = [fc.P(a + math.cos(2 * math.pi * (q * n + k) / seg) * r, y + math.sin(2 * math.pi * (q * n + k) / seg) * r, t + .02) for k in range(n + 1)]
        sk.tube(g, path, t, F['red' if q % 2 == 0 else 'hullwhite'], sides, caps=False)
    if T.i < 2:
        fc.box(g, a - .03, a + .03, y + r - .02, y + r + .1, 0, .12, F['ironplain'], T, bevel=.008)
        path = [fc.P(a + math.cos(2 * math.pi * k / 8) * (r + t + .04), y + math.sin(2 * math.pi * k / 8) * (r + t + .04) - .02, t + .02) for k in range(8)]
        sk.tube(g, path, .012, F['rope'], 3, closed=True)


def net(g, fc, a0, a1, y0, y1, T, seed):
    """A net hung to dry on a face: its mesh in sagging folds, cork floats along its head rope."""
    cols = (10, 5, 2)[T.i]
    rows = (5, 3, 1)[T.i]
    P = np.zeros((cols + 1, rows + 1, 3))
    for i in range(cols + 1):
        for j in range(rows + 1):
            u, v = i / cols, j / rows
            sag = .12 * math.sin(math.pi * u) * (1 - v) + .05 * math.sin(u * 9 + seed) * v
            d = .04 + .05 * (.5 + .5 * math.sin(u * 14 + v * 3 + seed))
            P[i, j] = fc.P(a0 + (a1 - a0) * u, y1 - (y1 - y0) * v - sag * (1 - v), d)
    c = fc.P((a0 + a1) / 2, (y0 + y1) / 2, -2)
    grid_surface(g, P, F['net'], inside=c)
    if T.i < 2:
        for i in range(0, cols + 1, 2):
            p = P[i, 0]
            lathe(g, [(.0, -.06), (.045, -.03), (.045, .03), (.0, .06)], (p[0] + fc.n[0] * .03, p[1], p[2] + fc.n[2] * .03), F['red' if i % 4 == 0 else 'plank'], 5, axis=fc.u)


def rope_line(g, pts, T, r=.03):
    """A mooring rope lying along a floor (or over an edge) through the points."""
    if T.i == 2:
        return
    sk.tube(g, sk.catmull(pts, per=(5, 3)[T.i]), r, F['rope'], (5, 4)[T.i])
