"""Polaris Station's finishes and the shapes its scripts share (docs/arenas/POLAR.md "Stage art", docs/arenas/STAGE-ART.md).
Run inside Blender with stagekit.py beside it. Everything is in the game's space (Unity: metres, +y up).

Shapes the stage kit lacks:
- `surf`: a grid of points as a smooth (or faceted) skin, its normals from the grid itself, turned to face a side it is given;
  a finish per face by its slope where asked (snow lies on what faces up, ice shows on what stands steep);
- `loft`: closed rings skinned into a body (rounded drifts, hulls, tanks, pods), capped;
- `rrect`, `ngon`: ring outlines; `mound`: a heap over a rectangle that keeps to its box;
- `shard`: a six-sided ice crystal with a pointed tip, leaning; `slab`: a jagged, faceted floe heaved on edge;
- `frame_window`: a lit window in a wall face (its reveal, bars and warm pane).
"""
import math

import numpy as np

import stagekit as sk
from stagekit import Geo, fin

F = {}


def finishes():
    """The stage's finishes (made once, in this order: the bank's vertex channels carry them)."""
    D = sk
    def f(name, *a, **k):
        F[name] = fin('polar-' + name, *a, **k)
    f('snow', 0xC2D2E4, D.DETAIL_WAX, smooth=.3, dirt=.25)
    f('snow-shade', 0xA2B8D0, D.DETAIL_WAX, smooth=.3, dirt=.25)
    f('ice', 0xA4D2EA, D.DETAIL_NONE, smooth=.82, dirt=.3)
    f('ice-deep', 0x4A8DB8, D.DETAIL_NONE, smooth=.9, dirt=.2)
    f('ice-dark', 0x2C5F88, D.DETAIL_NONE, smooth=.9, dirt=.2)
    f('ice-glow', 0x8FE8FF, glow=1.5)
    f('thin', 0x9CCFE8, D.DETAIL_NONE, smooth=.92, dirt=.15)
    f('seam', 0x2E6A98, D.DETAIL_NONE, smooth=.9, dirt=.1)
    f('water', 0x071C2A, D.DETAIL_NONE, smooth=.96, metal=.0, dirt=0)
    f('hull', 0x9A2F26, D.DETAIL_NONE, smooth=.5, metal=.25, dirt=.8, wear=.5)
    f('hull-dark', 0x1B1E26, D.DETAIL_NONE, smooth=.45, metal=.25, dirt=.6, wear=.4)
    f('white', 0xE4E2D8, D.DETAIL_NONE, smooth=.5, metal=.1, dirt=.7, wear=.2)
    f('deck', 0x8A7256, D.DETAIL_WOOD, smooth=.3, dirt=.9, param=2)
    f('timber', 0xA8703E, D.DETAIL_WOOD, smooth=.3, dirt=.8, param=0, wear=.2)
    f('timber-z', 0xA8703E, D.DETAIL_WOOD, smooth=.3, dirt=.8, param=2, wear=.2)
    f('timber-y', 0x8A5A36, D.DETAIL_WOOD, smooth=.3, dirt=.8, param=1, wear=.2)
    f('timber-dark', 0x5C3E28, D.DETAIL_WOOD, smooth=.3, dirt=.8, param=1)
    f('red-paint', 0x9A3A2E, D.DETAIL_NONE, smooth=.4, dirt=.8, wear=.3)
    f('blue-paint', 0x3F6A8A, D.DETAIL_NONE, smooth=.45, metal=.2, dirt=.8, wear=.3)
    f('steel', 0x8A96A2, D.DETAIL_NONE, smooth=.55, metal=.45, dirt=.7, wear=.4)
    f('steel-dark', 0x39424E, D.DETAIL_NONE, smooth=.5, metal=.45, dirt=.6, wear=.4)
    f('roof', 0x46586A, D.DETAIL_NONE, smooth=.5, metal=.35, dirt=.7, wear=.3)
    f('concrete', 0x8C96A4, D.DETAIL_PLASTER, smooth=.2, dirt=.9)
    f('concrete-dark', 0x626C7A, D.DETAIL_PLASTER, smooth=.2, dirt=.9)
    f('orange', 0xE8862A, D.DETAIL_NONE, smooth=.55, metal=.1, dirt=.7, wear=.3)
    f('red', 0xC8382C, D.DETAIL_NONE, smooth=.5, dirt=.6, wear=.2)
    f('yellow', 0xE0AC20, D.DETAIL_NONE, smooth=.5, metal=.2, dirt=.7, wear=.4)
    f('black', 0x14161A, D.DETAIL_NONE, smooth=.4, dirt=.4)
    f('rubber', 0x1C1C20, D.DETAIL_NONE, smooth=.25, dirt=.5)
    f('glass', 0x1A2C3C, D.DETAIL_NONE, smooth=.95, metal=.1, dirt=.2)
    f('tarp', 0x3F6A5C, D.DETAIL_CLOTH, smooth=.25, dirt=.8)
    f('rope', 0xB89A64, D.DETAIL_CLOTH, smooth=.15, dirt=.8)
    f('dish', 0xE8ECF2, D.DETAIL_NONE, smooth=.6, metal=.3, dirt=.4)
    f('balloon', 0xF4F0E6, D.DETAIL_WAX, smooth=.7, dirt=.1)
    f('drum', 0x2A5A8A, D.DETAIL_NONE, smooth=.5, metal=.3, dirt=.8, wear=.5)
    f('rock', 0x232A3E, D.DETAIL_NONE, smooth=.2, dirt=.5)
    f('window', 0xFFC88A, glow=2.1)
    f('window-dim', 0xFFB070, glow=1.25)
    f('lamp', 0xFFE2B0, glow=3.0)
    f('beacon', 0xFF3A2A, glow=3.0)
    f('screen', 0x6FE0FF, glow=1.6)
    f('green', 0x58F0B0, glow=1.5)
    f('ember', 0xFF7A30, glow=2.4)
    f('light', 0xFFFFFF, glow=1.0)           # tinted by its draw (the vents' lamps)


def H(a, b=0, c=0, d=0):
    """A stable 0..1 hash."""
    h = (int(a) * 73856093) ^ (int(b) * 19349663) ^ (int(c) * 83492791) ^ (int(d) * 2654435761)
    h = ((h ^ (h >> 13)) * 1274126177) & 0xFFFFFFFF
    return ((h ^ (h >> 16)) & 0xFFFF) / 65535.0


def noise(P, freq, seed, octaves=3):
    return sk.fbm(np.asarray(P, dtype=np.float64).reshape(-1, 3), freq, octaves, seed) - .5


# ---------------------------------------------------------------------------------------------------------------------------
# Skins
# ---------------------------------------------------------------------------------------------------------------------------
def surf(g, P, f, wrap_i=False, wrap_j=False, facing=None, inside=None, flat=False, pick=None):
    """A grid of points P (ni, nj, 3) as a skin. Its normals come from the grid; they are turned to agree with `facing` (a
    direction) or to point away from `inside` (a point). flat: every face its own normal (ice, rock). pick(normal, centre) -> a
    finish for a face (else `f`). wrap_i / wrap_j close the grid round."""
    P = np.asarray(P, dtype=np.float64)
    ni, nj = P.shape[:2]
    def d(axis, wrap):
        a = np.roll(P, -1, axis) - np.roll(P, 1, axis)
        if not wrap:
            if axis == 0:
                a[0] = P[1] - P[0]
                a[-1] = P[-1] - P[-2]
            else:
                a[:, 0] = P[:, 1] - P[:, 0]
                a[:, -1] = P[:, -1] - P[:, -2]
        return a
    N = np.cross(d(0, wrap_i), d(1, wrap_j))
    L = np.linalg.norm(N, axis=2, keepdims=True)
    N = np.where(L > 1e-9, N / np.maximum(L, 1e-9), np.array([0.0, 1.0, 0.0]))
    if inside is not None:
        s = np.sum(N * (P - np.asarray(inside, dtype=np.float64)), axis=2).sum()
    else:
        s = np.sum(N * np.asarray(facing if facing is not None else (0, 1, 0), dtype=np.float64), axis=2).sum()
    if s < 0:
        N = -N
    ci = ni if wrap_i else ni - 1
    cj = nj if wrap_j else nj - 1
    for i in range(ci):
        i1 = (i + 1) % ni
        for j in range(cj):
            j1 = (j + 1) % nj
            a, b, c, e = P[i, j], P[i1, j], P[i1, j1], P[i, j1]
            na, nb, nc, ne = N[i, j], N[i1, j], N[i1, j1], N[i, j1]
            hint = na + nb + nc + ne
            fn = np.cross(c - a, e - b)
            l = np.linalg.norm(fn)
            if l < 1e-12:
                continue
            fn = fn / l
            if np.dot(fn, hint) < 0:
                fn = -fn
            ff = f if pick is None else pick(fn, (a + b + c + e) * .25)
            pts = (tuple(a), tuple(b), tuple(c), tuple(e))
            if flat:
                g.face(pts, ff, normal=tuple(fn))
            else:
                g.face(pts, ff, normals=(tuple(na), tuple(nb), tuple(nc), tuple(ne)))


def loft(g, rings, f, cap0=True, cap1=True, flat=False, pick=None, f_cap=None):
    """Closed rings (each a list of points, the same count) skinned into a body; its ends capped flat."""
    P = np.asarray(rings, dtype=np.float64)
    inside = P.reshape(-1, 3).mean(axis=0)
    surf(g, P, f, wrap_j=True, inside=inside, flat=flat, pick=pick)
    fc = f if f_cap is None else f_cap
    if cap0:
        n = P[0].mean(axis=0) - P[min(1, len(P) - 1)].mean(axis=0)
        if np.linalg.norm(n) > 1e-9:
            g.face([tuple(p) for p in P[0]], fc, normal=tuple(n / np.linalg.norm(n)))
    if cap1:
        n = P[-1].mean(axis=0) - P[-2].mean(axis=0)
        if np.linalg.norm(n) > 1e-9:
            g.face([tuple(p) for p in P[-1]], fc, normal=tuple(n / np.linalg.norm(n)))


def rrect(hx, hz, r, n=3):
    """A rounded rectangle's outline in (x, z), anticlockwise from +x: n + 1 points a corner."""
    r = min(r, hx * .98, hz * .98)
    pts = []
    for q, (cx, cz) in enumerate(((hx - r, hz - r), (-(hx - r), hz - r), (-(hx - r), -(hz - r)), (hx - r, -(hz - r)))):
        for k in range(n + 1):
            a = (q + k / n) * math.pi / 2
            pts.append((cx + math.cos(a) * r, cz + math.sin(a) * r))
    return pts


def ngon(r, n, phase=0.0, sx=1.0, sz=1.0):
    return [(math.cos(2 * math.pi * k / n + phase) * r * sx, math.sin(2 * math.pi * k / n + phase) * r * sz) for k in range(n)]


def ring_at(outline, y, cx=0.0, cz=0.0, scale=1.0, sx=None, sz=None):
    sx = scale if sx is None else sx
    sz = scale if sz is None else sz
    return [(cx + x * sx, y, cz + z * sz) for x, z in outline]


def body(g, x0, x1, y0, y1, z0, z1, f, r=.12, n=3, T=None, top=None, f_top=None, taper=0.0):
    """A box with rounded upright corners and a softened top edge (a cabinet, a tank's skid, a vehicle's body): a loft of
    rounded rectangles. top: a finish for the lid. taper: how much narrower its top is (m a side)."""
    cx, cz, hx, hz = (x0 + x1) / 2, (z0 + z1) / 2, (x1 - x0) / 2, (z1 - z0) / 2
    n = n if (T is None or T.i == 0) else (2 if T.i == 1 else 1)
    o = rrect(hx, hz, r, n)
    e = min(r * .5, (y1 - y0) * .2)
    rings = [ring_at(o, y0, cx, cz), ring_at(o, y1 - e, cx, cz, sx=(hx - taper) / hx, sz=(hz - taper) / hz),
             ring_at(o, y1, cx, cz, sx=(hx - taper - e) / hx, sz=(hz - taper - e) / hz)]
    loft(g, rings, f, cap0=False, cap1=True, f_cap=f_top if f_top is not None else (top if top is not None else f))


def mound(g, x0, x1, z0, z1, y0, h, f, T, seed=0, steep=.22, bulge=.1, f_side=None, cell=None, rough=.05):
    """A heap over a rectangle that keeps to its box (a drift, a snow cap): its sides lean in by `steep` at most, its top swells
    by `bulge`, the wind has ruffled it."""
    c = cell or (.45, .9, 1.8)[T.i]
    nx = max(3, int(math.ceil((x1 - x0) / c)) + 1)
    nz = max(3, int(math.ceil((z1 - z0) / c)) + 1)
    U, V = np.meshgrid(np.linspace(-1, 1, nx), np.linspace(-1, 1, nz), indexing='ij')
    # a rounded plateau: the height falls to nothing over the last part of each side
    def edge(u, w):
        return np.clip((1 - np.abs(u)) / np.maximum(w, 1e-6), 0, 1) ** .5
    wx = min(.9, 2 * steep / max(x1 - x0, 1e-6) * 2.2)
    wz = min(.9, 2 * steep / max(z1 - z0, 1e-6) * 2.2)
    prof = np.minimum(edge(U, wx), edge(V, wz))
    X = (x0 + x1) / 2 + U * (x1 - x0) / 2
    Z = (z0 + z1) / 2 + V * (z1 - z0) / 2
    P = np.stack([X, np.zeros_like(X), Z], axis=2)
    n = noise(P, .9, seed + 11, 2).reshape(nx, nz)
    swell = (1 - U * U) * (1 - V * V)
    # (its hem a little under its floor: never in the floor's own plane)
    Y = y0 - .03 + prof * (h + .03 + bulge * swell + rough * 2 * n * (h > .25))
    P[:, :, 1] = Y
    fs = f if f_side is None else f_side
    surf(g, P, f, facing=(0, 1, 0), pick=(lambda nn, cc: f if nn[1] > .55 else fs) if f_side is not None else None)


def shard(g, base, tip, r, f, sides=6, phase=0.0, shoulder=.72, f_core=None, T=None):
    """An ice crystal: a six-sided prism from `base` toward `tip`, pointed over its last part; faceted. f_core: a lit seam down
    one edge."""
    b = np.asarray(base, dtype=np.float64)
    t = np.asarray(tip, dtype=np.float64)
    w = t - b
    L = np.linalg.norm(w)
    w = w / L
    ref = np.array([0.0, 1.0, 0.0]) if abs(w[1]) < .9 else np.array([1.0, 0.0, 0.0])
    u = np.cross(ref, w)
    u /= np.linalg.norm(u)
    v = np.cross(w, u)
    def ring(d, rr):
        return [tuple(b + w * d + (u * math.cos(2 * math.pi * k / sides + phase) + v * math.sin(2 * math.pi * k / sides + phase)) * rr) for k in range(sides)]
    r0, r1 = ring(0, r), ring(L * shoulder, r * .86)
    for k in range(sides):
        k1 = (k + 1) % sides
        g.face((r0[k], r0[k1], r1[k1], r1[k]), f, normal=None)
        g.face((r1[k], r1[k1], tuple(t)), f, normal=None)
    if f_core is not None and (T is None or T.detail):
        k = 0
        a, c = np.asarray(r0[k]), np.asarray(r1[k])
        out = (a - b)
        out = out / max(np.linalg.norm(out), 1e-9) * .012
        side = np.cross(w, out)
        side = side / max(np.linalg.norm(side), 1e-9) * (r * .1)
        g.face((tuple(a + out - side), tuple(a + out + side), tuple(c + out + side * .7), tuple(c + out - side * .7)), f_core, normal=tuple(out / np.linalg.norm(out)))


def _fix(g0, g, centre):
    """Faces of g0 into g, each turned to face away from `centre` (shards and slabs are made without a winding)."""
    P, N, S, Fi = g0.arrays()
    pos = 0
    c = np.asarray(centre, dtype=np.float64)
    for k, ff in zip(S, Fi):
        pts = P[pos:pos + k].astype(np.float64)
        pos += k
        n = np.zeros(3)
        for i in range(1, k - 1):
            n += np.cross(pts[i] - pts[0], pts[i + 1] - pts[0])
        if np.dot(n, pts.mean(axis=0) - c) < 0:
            n = -n
        l = np.linalg.norm(n)
        if l < 1e-12:
            continue
        g.face([tuple(p) for p in pts], int(ff), normal=tuple(n / l))


def crystal(g, base, tip, r, f, **k):
    """`shard`, its faces turned outward."""
    t = Geo()
    shard(t, base, tip, r, f, **k)
    b, tp = np.asarray(base, dtype=np.float64), np.asarray(tip, dtype=np.float64)
    # each face faces away from the crystal's own axis: turn them against the axis' nearest point
    P, N, S, Fi = t.arrays()
    pos = 0
    w = (tp - b) / np.linalg.norm(tp - b)
    for kk, ff in zip(S, Fi):
        pts = P[pos:pos + kk].astype(np.float64)
        pos += kk
        c = pts.mean(axis=0)
        on = b + w * np.dot(c - b, w)
        n = np.zeros(3)
        for i in range(1, kk - 1):
            n += np.cross(pts[i] - pts[0], pts[i + 1] - pts[0])
        out = c - on
        if np.linalg.norm(out) < 1e-6:
            out = w
        if np.dot(n, out) < 0:
            n = -n
        l = np.linalg.norm(n)
        if l > 1e-12:
            g.face([tuple(p) for p in pts], int(ff), normal=tuple(n / l))


def slab(g, x0, x1, y0, y1, z0, z1, f, f_top, T, seed, lean=.0, jag=.18, f_edge=None):
    """A floe heaved up on edge: a faceted block with a ragged outline that keeps within its box (its faces pushed in and out
    by `jag` at most, never past the box below 2 m), blue broken ice on its sides, snow on what faces up."""
    nx = max(2, int(round((x1 - x0) / (.8, 1.2, 2.4)[T.i])) + 1)
    nz = max(2, int(round((z1 - z0) / (.8, 1.2, 2.4)[T.i])) + 1)
    ny = max(2, int(round((y1 - y0) / (.9, 1.4, 3.0)[T.i])) + 1)
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    # a ring per height: the box's outline, sampled, pushed in by noise; the top rings draw in to a ragged crest
    out = []
    for i in range(nx):
        out.append((x0 + (x1 - x0) * i / (nx - 1), z0))
    for j in range(1, nz):
        out.append((x1, z0 + (z1 - z0) * j / (nz - 1)))
    for i in range(nx - 2, -1, -1):
        out.append((x0 + (x1 - x0) * i / (nx - 1), z1))
    for j in range(nz - 2, 0, -1):
        out.append((x0, z0 + (z1 - z0) * j / (nz - 1)))
    rings = []
    for k in range(ny):
        v = k / (ny - 1)
        y = y0 + (y1 - y0) * v
        ring = []
        for m, (x, z) in enumerate(out):
            d = jag * H(seed, k, m) * (1.0 if v > 0 else .3)
            crest = (v ** 4) * (.15 + .45 * H(seed, m, 7)) * min(x1 - x0, z1 - z0) * .5
            px = x + (cx - x) / max(abs(cx - x), abs(cz - z), 1e-6) * 0
            dx, dz = cx - x, cz - z
            l = math.hypot(dx, dz) or 1.0
            pull = d + crest
            ring.append((x + dx / l * pull + lean * v * (y1 - y0) * .0, y - (H(seed, m, 3) * .5 * (y1 - y0) * .22 if k == ny - 1 else 0.0), z + dz / l * pull))
        rings.append(ring)
    P = np.asarray(rings, dtype=np.float64)
    inside = (cx, (y0 + y1) / 2, cz)
    surf(g, P, f, wrap_j=True, inside=inside, flat=True, pick=lambda n, c: f_top if n[1] > .5 else (f_edge if (f_edge is not None and c[1] < y0 + (y1 - y0) * (.3 + .2 * H(seed, int(c[0] * 1.5), int(c[2] * 1.5)))) else f))
    top = [tuple(p) for p in P[-1]]
    c = P[-1].mean(axis=0)
    c[1] = P[-1][:, 1].max() + .12
    for m in range(len(top)):
        g.face((top[m], top[(m + 1) % len(top)], tuple(c)), f_top, normal=None if False else (0, 1, 0))


def frame_window(g, face, u0, u1, v0, v1, plane, T, f_frame, f_pane, depth=.08, bars=(1, 1), sill=True):
    """A lit window on a wall face. face: '+x' '-x' '+z' '-z' (the wall's outward side); u along the wall (z on x faces, x on z
    faces), v up; plane: the wall face's coordinate. The pane sits `depth` back inside a frame that stands a little proud."""
    s = 1.0 if face[0] == '+' else -1.0
    ax = face[1]
    def P(u, v, d):
        return (plane + s * d, v, u) if ax == 'x' else (u, v, plane + s * d)
    n = (s, 0, 0) if ax == 'x' else (0, 0, s)
    fw = .07
    # the frame: four bars proud of the wall
    for (a0, a1, b0, b1) in ((u0 - fw, u1 + fw, v1, v1 + fw), (u0 - fw, u1 + fw, v0 - fw, v0), (u0 - fw, u0, v0, v1), (u1, u1 + fw, v0, v1)):
        lo, hi = P(a0, b0, -.01), P(a1, b1, .045)
        sk.box(g, min(lo[0], hi[0]), max(lo[0], hi[0]), lo[1], hi[1], min(lo[2], hi[2]), max(lo[2], hi[2]), f_frame, T, bevel=.012, cell=0)
    if sill and T.detail:
        lo, hi = P(u0 - fw - .04, v0 - fw - .05, -.01), P(u1 + fw + .04, v0 - fw, .09)
        sk.box(g, min(lo[0], hi[0]), max(lo[0], hi[0]), lo[1], hi[1], min(lo[2], hi[2]), max(lo[2], hi[2]), f_frame, T, bevel=.012, cell=0)
    # the pane, a hair proud of the wall (the wall's face runs behind it), and its glazing bars
    g.face((P(u0, v0, .012), P(u1, v0, .012), P(u1, v1, .012), P(u0, v1, .012)), f_pane, normal=n)
    if T.detail:
        for k in range(1, bars[0] + 1):
            u = u0 + (u1 - u0) * k / (bars[0] + 1)
            lo, hi = P(u - .02, v0, .0), P(u + .02, v1, .035)
            sk.box(g, min(lo[0], hi[0]), max(lo[0], hi[0]), lo[1], hi[1], min(lo[2], hi[2]), max(lo[2], hi[2]), f_frame, T, bevel=0, cell=0)
        for k in range(1, bars[1] + 1):
            v = v0 + (v1 - v0) * k / (bars[1] + 1)
            lo, hi = P(u0, v - .02, .0), P(u1, v + .02, .035)
            sk.box(g, min(lo[0], hi[0]), max(lo[0], hi[0]), lo[1], hi[1], min(lo[2], hi[2]), max(lo[2], hi[2]), f_frame, T, bevel=0, cell=0)


def bx(b):
    """A layout block's box: x0, x1, y0, y1, z0, z1."""
    p, s = b['pos'], b['size']
    return (p[0] - s[0] / 2, p[0] + s[0] / 2, p[1] - s[1] / 2, p[1] + s[1] / 2, p[2] - s[2] / 2, p[2] + s[2] / 2)


def pipe(g, p0, p1, r, f, T, caps=True):
    sk.cylinder(g, p0, p1, r, f, (8, 6, 4)[T.i], caps=caps)


def mirror_x(g):
    """A Geo mirrored in x (the east half from the west): positions and normals negated in x, every face turned round."""
    P, N, S, Fi = g.arrays()
    o = Geo()
    pos = 0
    for k, ff in zip(S, Fi):
        pts = P[pos:pos + k].astype(np.float64).copy()
        nn = N[pos:pos + k].astype(np.float64).copy()
        pos += k
        pts[:, 0] *= -1
        nn[:, 0] *= -1
        o.face([tuple(p) for p in pts[::-1]], int(ff), normals=[tuple(n) for n in nn[::-1]])
    return o
