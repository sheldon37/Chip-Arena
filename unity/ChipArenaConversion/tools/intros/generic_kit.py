"""The shared kit of the generic intros' set scripts (generic_sets.py runs them): small maths, shapes, the pieces a set gives
the bank and the sky's recolouring, on stagekit (tools/stages/stagekit.py). Unity space: metres, +y up."""
import argparse
import math
import os
import sys
import time

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', 'stages'))
import numpy as np  # noqa: E402

import stagekit as sk  # noqa: E402
from stagekit import Geo, _add, _sub, _norm, _dot, _cross  # noqa: E402

BANK = 'ChipArenaIntroGeneric'
PI = math.pi
TAU = 2 * math.pi


# ---------------------------------------------------------------------------------------------------------------------------
# Small maths
# ---------------------------------------------------------------------------------------------------------------------------
def V(x, y, z):
    return (float(x), float(y), float(z))


def mul(a, k):
    return (a[0] * k, a[1] * k, a[2] * k)


def lerp(a, b, t):
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t)


def length(a):
    return math.sqrt(_dot(a, a))


def qmul(a, b):
    ax, ay, az, aw = a
    bx, by, bz, bw = b
    return (aw * bx + ax * bw + ay * bz - az * by, aw * by - ax * bz + ay * bw + az * bx,
            aw * bz + ax * by - ay * bx + az * bw, aw * bw - ax * bx - ay * by - az * bz)


def qeuler(yaw=0.0, pitch=0.0, roll=0.0):
    """Unity order: roll about z, then pitch about x, then yaw about y (radians)."""
    return qmul(sk.quat_axis(0, 1, 0, yaw), qmul(sk.quat_axis(1, 0, 0, pitch), sk.quat_axis(0, 0, 1, roll)))


def M(t=(0, 0, 0), yaw=0.0, pitch=0.0, roll=0.0, s=1.0):
    ss = s if isinstance(s, tuple) else (s, s, s)
    return sk.affine(qeuler(yaw, pitch, roll), t, ss)


def place(g, sub, m):
    g.extend(sub.transformed(m))


def hash01(*k):
    h = 2166136261
    for v in k:
        for ch in str(v):
            h = ((h ^ ord(ch)) * 16777619) & 0xffffffff
    return (h & 0xffffff) / float(0xffffff)


def noise2(x, z, freq, seed, octaves=3):
    return float(sk.fbm(np.array([[x, 0.0, z]]), freq, octaves, seed)[0])


# ---------------------------------------------------------------------------------------------------------------------------
# Shapes (Unity space)
# ---------------------------------------------------------------------------------------------------------------------------
def quad_strip_grid(g, pts, fins, flat=True, normals=None, flip=False):
    """A grid of points pts[i][j] (rows i, columns j) as quads; fins[i][j] the finish of quad (i, j) (or one finish);
    flat: each quad's own normal; normals[i][j]: smooth per corner. flip turns every face round."""
    rows, cols = len(pts), len(pts[0])
    for i in range(rows - 1):
        for j in range(cols - 1):
            a, b, c, d = pts[i][j], pts[i][j + 1], pts[i + 1][j + 1], pts[i + 1][j]
            f = fins if isinstance(fins, int) else fins[i][j]
            if normals is not None and not flat:
                na, nb, nc, nd = normals[i][j], normals[i][j + 1], normals[i + 1][j + 1], normals[i + 1][j]
                if flip:
                    g.face((d, c, b, a), f, normals=(na, nb, nc, nd)[::-1])
                else:
                    g.face((a, b, c, d), f, normals=(na, nb, nc, nd))
            else:
                n = _cross(_sub(b, a), _sub(d, a))
                if flip:
                    n = mul(n, -1)
                if length(n) < 1e-12:
                    continue
                # split a non-planar quad along its shorter diagonal so the facets stay flat
                g.face((a, b, c), f, normal=_cross(_sub(b, a), _sub(c, a)) if not flip else mul(_cross(_sub(b, a), _sub(c, a)), -1))
                g.face((a, c, d), f, normal=_cross(_sub(c, a), _sub(d, a)) if not flip else mul(_cross(_sub(c, a), _sub(d, a)), -1))


def heightfield(g, x0, x1, z0, z1, nx, nz, h, fin_of, smooth=True):
    """y = h(x, z) over a grid; fin_of(x, y, z, n) picks each quad's finish; smooth normals from the heights (or faceted)."""
    xs = [x0 + (x1 - x0) * i / nx for i in range(nx + 1)]
    zs = [z0 + (z1 - z0) * j / nz for j in range(nz + 1)]
    H = [[h(x, z) for x in xs] for z in zs]
    pts = [[V(xs[i], H[j][i], zs[j]) for i in range(nx + 1)] for j in range(nz + 1)]
    nrm = None
    if smooth:
        nrm = []
        for j in range(nz + 1):
            row = []
            for i in range(nx + 1):
                hl = H[j][max(0, i - 1)]
                hr = H[j][min(nx, i + 1)]
                hd = H[max(0, j - 1)][i]
                hu = H[min(nz, j + 1)][i]
                dx = xs[min(nx, i + 1)] - xs[max(0, i - 1)]
                dz = zs[min(nz, j + 1)] - zs[max(0, j - 1)]
                row.append(_norm((-(hr - hl) / dx, 1.0, -(hu - hd) / dz)))
            nrm.append(row)
    fins = []
    for j in range(nz):
        row = []
        for i in range(nx):
            c = mul(_add(_add(pts[j][i], pts[j][i + 1]), _add(pts[j + 1][i], pts[j + 1][i + 1])), .25)
            n = _norm(_cross(_sub(pts[j + 1][i], pts[j][i]), _sub(pts[j][i + 1], pts[j][i])))
            if n[1] < 0:
                n = mul(n, -1)
            row.append(fin_of(c[0], c[1], c[2], n))
        fins.append(row)
    # rows run along +z, columns along +x: (a, b, c, d) = (z, x) (z, x+1) (z+1, x+1) (z+1, x) winds clockwise seen from above,
    # whose cross product points down; flip so the faces look up
    quad_strip_grid(g, pts, fins, flat=not smooth, normals=nrm, flip=True)


def polar_field(g, centre, r0, r1, nr, nt, h, fin_of, smooth=False, rpow=1.0, theta0=0.0, theta1=TAU):
    """y = h(r, theta, x, z) over an annulus round centre (x, z); rings spaced by rpow (>1: denser inside)."""
    cx, cz = centre
    full = abs(theta1 - theta0 - TAU) < 1e-6
    nt1 = nt if full else nt + 1
    rs = [r0 + (r1 - r0) * (i / nr) ** rpow for i in range(nr + 1)]
    ts = [theta0 + (theta1 - theta0) * j / nt for j in range(nt1)]
    P = []
    for r in rs:
        row = []
        for t in ts:
            x, z = cx + math.cos(t) * r, cz + math.sin(t) * r
            row.append(V(x, h(r, t, x, z), z))
        if full:
            row.append(row[0])
        P.append(row)
    cols = len(P[0])
    fins = []
    for i in range(nr):
        row = []
        for j in range(cols - 1):
            c = mul(_add(_add(P[i][j], P[i][j + 1]), _add(P[i + 1][j], P[i + 1][j + 1])), .25)
            n = _norm(_cross(_sub(P[i][j + 1], P[i][j]), _sub(P[i + 1][j], P[i][j])))
            if n[1] < 0:
                n = mul(n, -1)
            row.append(fin_of(c[0], c[1], c[2], n))
        fins.append(row)
    nrm = None
    if smooth:
        nrm = []
        for i in range(nr + 1):
            row = []
            for j in range(cols):
                a = P[max(0, i - 1)][j]
                b = P[min(nr, i + 1)][j]
                c = P[i][(j - 1) % (cols - 1) if full else max(0, j - 1)]
                d = P[i][(j + 1) % (cols - 1) if full else min(cols - 1, j + 1)]
                n = _norm(_cross(_sub(d, c), _sub(b, a)))
                if n[1] < 0:
                    n = mul(n, -1)
                row.append(n)
            nrm.append(row)
    # rings outward (i), angle round (j): (r,t) (r,t+1) (r+1,t+1) (r+1,t) winds anticlockwise from above in Unity (+x, +z):
    # its cross product points up for theta increasing from +x toward +z
    quad_strip_grid(g, P, fins, flat=not smooth, normals=nrm, flip=False)


def rockball(g, centre, radii, f, seed, lat=6, lon=9, rough=.22, flatten=0.0):
    """A faceted boulder: a lat-long ball displaced by noise, flat shaded; flatten squashes its underside into the ground."""
    cx, cy, cz = centre
    P = []
    for i in range(lat + 1):
        phi = -PI / 2 + PI * i / lat
        row = []
        for j in range(lon):
            th = TAU * j / lon + seed
            d = (math.cos(phi) * math.cos(th), math.sin(phi), math.cos(phi) * math.sin(th))
            k = 1 + rough * (2 * hash01(seed, i, j) - 1)
            y = d[1] * radii[1] * k
            if y < -radii[1] * (1 - flatten):
                y = -radii[1] * (1 - flatten)
            row.append(V(cx + d[0] * radii[0] * k, cy + y, cz + d[2] * radii[2] * k))
        row.append(row[0])
        P.append(row)
    for i in range(lat):
        for j in range(lon):
            a, b, c, d = P[i][j], P[i][j + 1], P[i + 1][j + 1], P[i + 1][j]
            for tri in ((a, b, c), (a, c, d)):
                n = _cross(_sub(tri[1], tri[0]), _sub(tri[2], tri[0]))
                if length(n) < 1e-10:
                    continue
                mid = mul(_add(_add(tri[0], tri[1]), tri[2]), 1 / 3.0)
                out = _sub(mid, centre)
                if _dot(n, out) < 0:
                    n = mul(n, -1)
                g.face(tri, f, normal=n)


def mound(g, centre, radii, f, seed, lat=6, lon=12, rough=.12, flatten=.55, smooth=True):
    """A soft lump (a snow drift, a cloud's puff): a lat-long ball, noise-displaced, its underside cut flat, smooth shaded."""
    cx, cy, cz = centre
    P, Nn = [], []
    for i in range(lat + 1):
        phi = -PI / 2 + PI * i / lat
        row, nrow = [], []
        for j in range(lon):
            th = TAU * j / lon + seed
            d = (math.cos(phi) * math.cos(th), math.sin(phi), math.cos(phi) * math.sin(th))
            k = 1 + rough * (2 * hash01(seed, i, j % lon) - 1) * (1 if 0 < i < lat else 0)
            y = d[1] * radii[1] * k
            lo = -radii[1] * (1 - flatten)
            y = max(y, lo)
            row.append(V(cx + d[0] * radii[0] * k, cy + y, cz + d[2] * radii[2] * k))
            nrow.append(_norm((d[0] / radii[0], d[1] / radii[1], d[2] / radii[2])))
        row.append(row[0])
        nrow.append(nrow[0])
        P.append(row)
        Nn.append(nrow)
    for i in range(lat):
        for j in range(lon):
            a, b, c, d = P[i][j], P[i][j + 1], P[i + 1][j + 1], P[i + 1][j]
            na, nb, nc, nd = Nn[i][j], Nn[i][j + 1], Nn[i + 1][j + 1], Nn[i + 1][j]
            if smooth:
                if i == lat - 1:
                    g.face((a, b, c), f, normals=(na, nb, nc))
                elif i == 0:
                    g.face((a, c, d), f, normals=(na, nc, nd))
                else:
                    g.face((a, b, c, d), f, normals=(na, nb, nc, nd))
            else:
                mid = mul(_add(_add(a, b), _add(c, d)), .25)
                g.face((a, b, c, d), f, normal=_sub(mid, centre))


def dome(g, r, f, rings=10, segs=32, lo=-12.0, hi=90.0, centre=(0, 0, 0)):
    """A sky dome seen from inside (normals inward), from `lo` to `hi` degrees of elevation."""
    P = []
    for i in range(rings + 1):
        el = math.radians(lo + (hi - lo) * i / rings)
        row = []
        for j in range(segs + 1):
            az = TAU * j / segs
            row.append(V(centre[0] + r * math.cos(el) * math.sin(az), centre[1] + r * math.sin(el), centre[2] + r * math.cos(el) * math.cos(az)))
        P.append(row)
    for i in range(rings):
        for j in range(segs):
            a, b, c, d = P[i][j], P[i][j + 1], P[i + 1][j + 1], P[i + 1][j]
            mid = mul(_add(_add(a, b), _add(c, d)), .25)
            inward = _norm(_sub(centre, mid))
            if i == rings - 1 and hi >= 89.9:
                g.face((a, b, c), f, normal=inward)
            else:
                g.face((a, b, c, d), f, normal=inward)


def disc(g, centre, r, f, n=24, normal=(0, 1, 0), r_in=0.0, phase=0.0):
    """A flat disc (or annulus with r_in) facing `normal`."""
    u, v, w = sk.frame((0, 0, 0), normal)
    pts = [_add(_add(centre, u, r * math.cos(TAU * k / n + phase)), v, r * math.sin(TAU * k / n + phase)) for k in range(n)]
    if r_in <= 0:
        for k in range(n):
            g.face((centre, pts[k], pts[(k + 1) % n]), f, normal=normal)
    else:
        inn = [_add(_add(centre, u, r_in * math.cos(TAU * k / n + phase)), v, r_in * math.sin(TAU * k / n + phase)) for k in range(n)]
        for k in range(n):
            g.face((inn[k], pts[k], pts[(k + 1) % n], inn[(k + 1) % n]), f, normal=normal)


def ribbed(g, origin, u, v, ribs, amp, f, steps_v=2, normal=None):
    """A corrugated sheet: origin + s u + t v, rippled along u (`ribs` waves of depth `amp` along the normal u x v, or `normal`)."""
    n = _norm(normal if normal is not None else _cross(u, v))
    k = ribs * 4
    cols = []
    for i in range(k + 1):
        s = i / k
        off = amp * math.sin(TAU * ribs * s)
        cols.append((s, off, math.cos(TAU * ribs * s)))
    pts, nrm = [], []
    for j in range(steps_v + 1):
        t = j / steps_v
        row, nr = [], []
        for s, off, slope in cols:
            row.append(_add(_add(_add(origin, u, s), v, t), n, off))
            # the normal tilts with the ripple's slope across u
            du = _norm(u)
            tilt = -amp * TAU * ribs / max(1e-6, length(u)) * slope
            nr.append(_norm(_add(n, du, tilt)))
        pts.append(row)
        nrm.append(nr)
    for j in range(steps_v):
        for i in range(k):
            a, b, c, d = pts[j][i], pts[j][i + 1], pts[j + 1][i + 1], pts[j + 1][i]
            g.face((a, b, c, d), f, normals=(nrm[j][i], nrm[j][i + 1], nrm[j + 1][i + 1], nrm[j + 1][i]))


def catenary(p0, p1, sag, n=8):
    out = []
    for i in range(n + 1):
        t = i / n
        p = lerp(p0, p1, t)
        out.append(V(p[0], p[1] - sag * 4 * t * (1 - t), p[2]))
    return out


def lattice_tower(g, base, height, r0, r1, step, T, legs_f, tie_f=None, legs=3, band=None, yaw=0.0, leg_r=.055, tie_r=.03):
    """A lattice mast: `legs` legs from radius r0 at the base to r1 at the top, horizontal ties every `step` m and X bracing
    on each face (Fine only). band(i) may give section i's finish (aviation paint)."""
    tie_f = legs_f if tie_f is None else tie_f
    n = max(1, int(round(height / step)))
    sides_leg = 6 if T.i == 0 else 4
    sides_tie = 5 if T.i == 0 else 3

    def corner(k, y):
        r = r0 + (r1 - r0) * (y / height)
        a = yaw + TAU * k / legs
        return V(base[0] + r * math.sin(a), base[1] + y, base[2] + r * math.cos(a))
    for i in range(n):
        y0, y1 = height * i / n, height * (i + 1) / n
        fsec = band(i) if band else legs_f
        for k in range(legs):
            sk.cylinder(g, corner(k, y0), corner(k, y1), leg_r, fsec, sides_leg, caps=False)
            a0, a1 = corner(k, y1), corner((k + 1) % legs, y1)
            sk.cylinder(g, a0, a1, tie_r, band(i) if band else tie_f, sides_tie, caps=False)
            if T.i == 0:
                b0, b1 = corner(k, y0), corner((k + 1) % legs, y0)
                sk.cylinder(g, b0, a1, tie_r * .8, tie_f if not band else fsec, 4, caps=False)
                sk.cylinder(g, b1, a0, tie_r * .8, tie_f if not band else fsec, 4, caps=False)
    return [corner(k, height) for k in range(legs)]


def flipped(src):
    """A copy of a Geo with every face turned round (winding and normals)."""
    g = Geo()
    P, N, S, F = src.arrays()
    off = 0
    for i, k in enumerate(S):
        k = int(k)
        pts = [tuple(float(c) for c in P[off + m]) for m in range(k)][::-1]
        nrm = [tuple(-float(c) for c in N[off + m]) for m in range(k)][::-1]
        g.face(pts, int(F[i]), normals=nrm)
        off += k
    return g


def dish(g, centre, facing_yaw, tilt, r, f_face, f_back, f_arm, T, depth=.28):
    """A parabolic dish antenna at `centre`, its axis turned by yaw and tilted up by `tilt`, with a feed on three struts."""
    sides = 18 if T.i == 0 else 8
    prof = [(max(.001, r * i / 5), depth * (i / 5) ** 2) for i in range(6)]
    # the dish opens along +z (local); stagekit's lathe faces the convex back, so the bowl's face is a turned copy
    front = Geo()
    sk.lathe(front, prof, (0, 0, 0), f_face, sides, axis=(0, 0, 1))
    sub = flipped(front)
    sk.lathe(sub, [(a, b - .03) for a, b in prof], (0, 0, 0), f_back, sides, axis=(0, 0, 1))
    feed = (0, 0, depth + r * r / (4 * depth) * .55)
    for k in range(3):
        a = TAU * k / 3
        rim = (r * .96 * math.cos(a), r * .96 * math.sin(a), depth * .92)
        sk.cylinder(sub, rim, feed, .012, f_arm, 4, caps=False)
    sk.cylinder(sub, feed, _add(feed, (0, 0, .12)), .05, f_arm, 6)
    sk.cylinder(sub, (0, 0, -.03), (0, 0, -.35), .06, f_arm, 6)
    place(g, sub, M(centre, yaw=facing_yaw, pitch=-tilt))


def windowed_band(g, x0, x1, y0, y1, z, f_glass, f_frame, mullion=1.6, outward=1):
    """A strip of window on a wall plane z (facing +z for outward 1), mullions every `mullion` m."""
    n = max(1, int(round((x1 - x0) / mullion)))
    for i in range(n):
        a = x0 + (x1 - x0) * i / n
        b = x0 + (x1 - x0) * (i + 1) / n
        g.face((V(a + .04, y0, z), V(b - .04, y0, z), V(b - .04, y1, z), V(a + .04, y1, z)), f_glass, normal=(0, 0, outward))
        sk.box(g, a - .04, a + .04, y0 - .04, y1 + .04, z - .03, z + .03 * outward, f_frame, None, .005, cell=0)


# ---------------------------------------------------------------------------------------------------------------------------
# Tiers
# ---------------------------------------------------------------------------------------------------------------------------
def tier(lod):
    """lod 0 Fine (the intro's close camera), 1 Low (the Low graphics tier): stagekit's tiers with coarser flat cells (the
    banks stay small; the stage shader's fine surfaces carry the close detail)."""
    T = sk.Tier(0 if lod == 0 else 2)
    T.cell = 1.2 if lod == 0 else 6.0
    return T


# ---------------------------------------------------------------------------------------------------------------------------
# Recolouring hooks (sky gradients, the aurora's curtains): run on a Bake after it is made
# ---------------------------------------------------------------------------------------------------------------------------
def recolour(g, bk, fins, fn):
    """Replace the albedo of the corners whose finish is in `fins` by fn(P (n,3)) -> sRGB (n,3) in 0..1."""
    P, N, S, F = g.arrays()
    fc = np.repeat(F, S)
    sel = np.isin(fc, np.array(sorted(fins), dtype=np.int64))
    if not sel.any():
        return
    rgb = np.clip(fn(P[sel].astype(np.float64)), 0, 1)
    bk.colour[sel, :3] = np.round(rgb * 255).astype(np.uint8)
    bk.colour[sel, 3] = 255


def mix_hex(a, b, t):
    ca, cb = np.array(sk.srgb(a)), np.array(sk.srgb(b))
    t = np.clip(np.asarray(t, dtype=np.float64), 0, 1)[..., None]
    return ca * (1 - t) + cb * t


def sky_fn(zenith, horizon, sun_dir, sun_col, sun_width=.9, below=None):
    """A sky gradient: zenith to horizon by elevation, warmed toward the sun's azimuth (unit xz `sun_dir`)."""
    def fn(P):
        d = P / np.maximum(np.linalg.norm(P, axis=1, keepdims=True), 1e-9)
        el = np.clip(d[:, 1], -1, 1)
        up = np.clip(el, 0, 1) ** .55
        base = mix_hex(horizon, zenith, up)
        xz = d[:, [0, 2]]
        xz = xz / np.maximum(np.linalg.norm(xz, axis=1, keepdims=True), 1e-9)
        toward = np.clip((xz @ np.array(sun_dir)) * .5 + .5, 0, 1) ** (1.0 / max(.1, sun_width))
        warm = toward * np.clip(1 - up * 1.6, 0, 1)
        out = base * (1 - warm[:, None]) + np.array(sk.srgb(sun_col)) * warm[:, None]
        if below is not None:
            dn = np.clip(-el * 6, 0, 1)
            out = out * (1 - dn[:, None]) + np.array(sk.srgb(below)) * dn[:, None]
        return out
    return fn


# ---------------------------------------------------------------------------------------------------------------------------
# The pieces a set gives the bank
# ---------------------------------------------------------------------------------------------------------------------------
class Piece:
    """One bank prop: its Geo, whether Cycles bakes its occlusion, the floors its grime rises from, a recolour hook
    (fins, fn), and optional extra placements of the same mesh (3x4 rows) drawn as one prop."""

    def __init__(self, name, g, ao=True, floors=(0.0,), recolour=None, places=None, seed=0, preview=None):
        self.name, self.g, self.ao, self.floors, self.recolour, self.places, self.seed = name, g, ao, floors, recolour, places, seed
        # where the preview draws it: None as built, False not at all, or a list of 3x4 placements (a moving piece's pivot)
        self.preview = preview


def smoothstep(a, b, x):
    t = min(1.0, max(0.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


def ang_diff(a, b):
    return (a - b + PI) % TAU - PI
