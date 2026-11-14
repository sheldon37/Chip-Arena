"""Skyframe's finishes and the shapes its scripts share (docs/arenas/SKYFRAME.md "Stage art", docs/arenas/STAGE-ART.md): beams
laid between two points, I-sections, slabs with their own top, side and soffit finishes, lumpy blobs with smooth normals (leaf
masses, shrubs, clouds), leafy boxes (hedges, living walls), glass panes. Everything in the game's space (Unity, metres, +y up).
Run inside Blender with stagekit.py beside it."""
import math

import numpy as np

import stagekit as sk
from stagekit import Geo, box, lathe, grid_face

F = {}


def finishes():
    D = sk
    def f(name, hexcol, **k):
        F[name] = sk.fin('sky-' + name, hexcol, **k)
    f('paving', 0xD9CEB9, detail=D.DETAIL_FLAG, smooth=.34, dirt=.55, param=1.0)
    f('paving-dark', 0xA89D89, detail=D.DETAIL_FLAG, smooth=.3, dirt=.55, param=1.0)
    f('stone', 0xE4DAC6, smooth=.28, dirt=.5)
    f('coping', 0xF0E8D8, smooth=.42, dirt=.35, wear=.2)
    f('concrete', 0xC8C0B2, detail=D.DETAIL_PLASTER, smooth=.14, dirt=.7)
    f('concrete-dark', 0x8F897E, detail=D.DETAIL_PLASTER, smooth=.12, dirt=.6)
    f('steel', 0x4B4F58, smooth=.6, metal=.5, dirt=.3, wear=.3)
    f('stainless', 0xC9CED2, smooth=.86, metal=.5, dirt=.15)
    f('primer', 0xB5553A, smooth=.32, metal=.2, dirt=.5, wear=.25)
    f('deck', 0xAEB4B8, smooth=.55, metal=.45, dirt=.4)
    f('glass', 0xA8E2E8, smooth=.96, metal=.35, dirt=.08)
    f('glass-dark', 0x4F8A9A, smooth=.96, metal=.4, dirt=.1)
    f('glass-floor', 0xB9E3E7, smooth=.92, metal=.3, dirt=.12)
    f('water', 0x2F6F7C, smooth=.98, metal=.35, dirt=0)
    f('timber', 0xB88955, detail=D.DETAIL_WOOD, smooth=.3, dirt=.45, param=1.0)
    f('timber-dark', 0x7A5637, detail=D.DETAIL_WOOD, smooth=.28, dirt=.5, param=1.0)
    f('bark', 0x6B4B34, detail=D.DETAIL_WOOD, smooth=.12, dirt=.5, param=1.0)
    f('soil', 0x4B372A, smooth=.05, dirt=.3)
    f('lawn', 0x7DAE4E, smooth=.06, dirt=.25)
    f('lawn-dark', 0x669A44, smooth=.06, dirt=.25)
    f('leaf', 0x5F9C47, smooth=.18, dirt=.15)
    f('leaf-dark', 0x3E7437, smooth=.14, dirt=.15)
    f('leaf-light', 0x8DBE5A, smooth=.22, dirt=.1)
    f('leaf-gold', 0xD0B44C, smooth=.22, dirt=.1)
    f('flower-pink', 0xF2A0B4, smooth=.2, dirt=.05)
    f('flower-white', 0xFFF4E0, smooth=.2, dirt=.05)
    f('flower-orange', 0xFF9A3C, smooth=.2, dirt=.05)
    f('crane', 0xF2B21E, smooth=.45, metal=.2, dirt=.35, wear=.35)
    f('crane-dark', 0x2D2F34, smooth=.4, metal=.3, dirt=.3, wear=.2)
    f('weight', 0xA9A49A, detail=D.DETAIL_PLASTER, smooth=.12, dirt=.6)
    f('cable', 0x1E1E20, smooth=.5, metal=.5, dirt=.1)
    f('paint-yellow', 0xF2B424, smooth=.3, dirt=.45)
    f('paint-red', 0xD8452E, smooth=.3, dirt=.4)
    f('paint-white', 0xF2EEE2, smooth=.3, dirt=.4)
    f('net', 0xFF7A1A, detail=D.DETAIL_CLOTH, smooth=.1, dirt=.3)
    f('tarp', 0x3F7FB8, detail=D.DETAIL_CLOTH, smooth=.25, dirt=.35)
    f('cloth', 0xF3E9D4, detail=D.DETAIL_CLOTH, smooth=.3, dirt=.12)
    f('cloth-orange', 0xE8742A, detail=D.DETAIL_CLOTH, smooth=.3, dirt=.12)
    f('white', 0xF4F2EC, smooth=.6, dirt=.12)
    f('cloud-top', 0xFFF6EA, smooth=.02, dirt=0)
    f('cloud-mid', 0xFFE4D0, smooth=.02, dirt=0)
    f('cloud-low', 0xDDA8B4, smooth=.02, dirt=0)
    f('bird', 0x2B2B33, smooth=.2, dirt=0)
    f('bird-light', 0xE9E4DA, smooth=.2, dirt=0)
    f('bronze', 0xB98A4A, smooth=.7, metal=.5, dirt=.25, wear=.2)
    f('lamp', 0xFFC680, glow=2.6)
    f('lamp-white', 0xFFF0D8, glow=2.0)
    f('lamp-soft', 0xFFD8A0, glow=1.2)
    f('beacon', 0xFF3A2A, glow=3.0)
    f('window', 0xFFC884, glow=1.3)
    f('screen', 0x8FE6F0, glow=1.6)


def hashf(*k):
    """A stable 0..1 hash of numbers (the scripts' variation: the same layout builds the same bank)."""
    h = 2166136261
    for v in k:
        h = (h ^ (int(round(v * 131)) & 0xFFFFFFFF)) * 16777619 & 0xFFFFFFFF
        h ^= h >> 13
    h = (h * 2654435761) & 0xFFFFFFFF
    return ((h ^ (h >> 15)) & 0xFFFFFF) / float(0x1000000)


def blk(L, i):
    b = L['byid'][i]
    p, s = b['pos'], b['size']
    return (p[0] - s[0] / 2, p[0] + s[0] / 2, p[1] - s[1] / 2, p[1] + s[1] / 2, p[2] - s[2] / 2, p[2] + s[2] / 2)


def index(L):
    L['byid'] = {b['id']: b for b in L['blocks']}
    return L


def ids(L, prefix):
    return [b['id'] for b in L['blocks'] if b['id'].startswith(prefix)]


def _sub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def _cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def _norm(a):
    l = math.sqrt(a[0] * a[0] + a[1] * a[1] + a[2] * a[2]) or 1.0
    return (a[0] / l, a[1] / l, a[2] / l)


def along(p0, p1, up=(0, 1, 0)):
    """The 3x4 rows taking a local frame (x across, y up-ish, z from 0 to the length) onto the line p0 -> p1, and the length."""
    d = _sub(p1, p0)
    L = math.sqrt(d[0] * d[0] + d[1] * d[1] + d[2] * d[2])
    w = _norm(d)
    ref = up if abs(w[0] * up[0] + w[1] * up[1] + w[2] * up[2]) < .96 else (1, 0, 0)
    u = _norm(_cross(ref, w))
    v = _cross(w, u)
    return [u[0], v[0], w[0], p0[0], u[1], v[1], w[1], p0[1], u[2], v[2], w[2], p0[2]], L


def beam(g, p0, p1, w, h, f, T=None, bevel=.0, up=(0, 1, 0)):
    """A rectangular bar w wide, h deep, laid from p0 to p1 (its section centred on the line)."""
    m, L = along(p0, p1, up)
    if L < 1e-4:
        return
    t = Geo()
    box(t, -w / 2, w / 2, -h / 2, h / 2, 0, L, f, None, bevel, cell=0)
    g.extend(t.transformed(m))


def ibeam(g, p0, p1, w, h, f, T, up=(0, 1, 0), tf=None, tw=None):
    """An I-section w wide, h deep from p0 to p1 (the line runs through its middle): two flanges and the web; a plain bar on the
    Low tier."""
    if T is not None and T.i == 2:
        beam(g, p0, p1, w * .7, h, f, None, 0, up)
        return
    tf = tf or max(.025, h * .09)
    tw = tw or max(.02, w * .1)
    m, L = along(p0, p1, up)
    if L < 1e-4:
        return
    t = Geo()
    box(t, -w / 2, w / 2, h / 2 - tf, h / 2, 0, L, f, None, 0, cell=0)
    box(t, -w / 2, w / 2, -h / 2, -h / 2 + tf, 0, L, f, None, 0, cell=0)
    box(t, -tw / 2, tw / 2, -h / 2 + tf, h / 2 - tf, 0, L, f, None, 0, cell=0, skip=('+y', '-y'))
    g.extend(t.transformed(m))


def rod(g, p0, p1, r, f, sides, caps=False):
    sk.cylinder(g, p0, p1, r, f, sides, caps=caps)


def slab(g, b, T, top, side, bottom=None, cell=None, bevel=0.0):
    """A block as a slab: its top, its sides and its soffit each their own finish."""
    x0, x1, y0, y1, z0, z1 = b
    c = T.cell if cell is None else (cell or 1e6)
    grid_face(g, (x0, y1, z0), (x1 - x0, 0, 0), (0, 0, z1 - z0), 0, 0, top, (0, 1, 0), c)
    for o, u, n in (((x0, y0, z0), (x1 - x0, 0, 0), (0, 0, -1)), ((x0, y0, z1), (x1 - x0, 0, 0), (0, 0, 1)), ((x0, y0, z0), (0, 0, z1 - z0), (-1, 0, 0)), ((x1, y0, z0), (0, 0, z1 - z0), (1, 0, 0))):
        grid_face(g, o, u, (0, y1 - y0, 0), 0, 0, side, n, max(c, 2.0))
    grid_face(g, (x0, y0, z0), (x1 - x0, 0, 0), (0, 0, z1 - z0), 0, 0, bottom if bottom is not None else side, (0, -1, 0), max(c, 3.0))


# An inlay's layer: how far each layer of flat detail stands over the one under it, by tier (stagekit.LIFT_TIER: clear of the
# depth buffer at the tier's range). The scripts stack their inlays themselves (a lawn over its kerb over the paving), so the
# order is theirs, not the faces' areas'; stagekit.lift_coplanar then finds nothing left to settle among them.
LAYER = [sk.LIFT_TIER[0]]


def ly(k):
    return k * LAYER[0]


def rect(g, x0, x1, y, z0, z1, f, cell=0, layer=1):
    """A flat rectangle on a floor at height y, facing up, `layer` layers over it."""
    y = y + ly(layer)
    if cell:
        grid_face(g, (x0, y, z0), (x1 - x0, 0, 0), (0, 0, z1 - z0), 0, 0, f, (0, 1, 0), cell)
    else:
        g.face(((x0, y, z0), (x1, y, z0), (x1, y, z1), (x0, y, z1)), f, (0, 1, 0))


def blob(g, c, r, T, seed, fins, squash=(1.0, 1.0, 1.0), lumps=.22, freq=1.6, cut=None, sides=None):
    """A lumpy ball with smooth normals: centre c, radius r, stretched by `squash`; `fins` (top, middle, underside) by the way a
    face looks; `cut`: the height (in radii below the centre) under which it is flattened (a cloud's base, a shrub on its soil)."""
    nu = sides or (12, 8, 6)[T.i]
    nv = max(3, nu // 2 + 1)
    P = np.zeros((nv + 1, nu, 3))
    N = np.zeros((nv + 1, nu, 3))
    for j in range(nv + 1):
        th = math.pi * j / nv
        for i in range(nu):
            ph = 2 * math.pi * i / nu
            d = np.array([math.sin(th) * math.cos(ph), math.cos(th), math.sin(th) * math.sin(ph)])
            k = 1.0 + lumps * (sk.fbm(np.array([[d[0] * freq + seed * 3.1, d[1] * freq + seed * 1.7, d[2] * freq - seed * 2.3]]), 1.0, 2, int(seed * 13) % 97)[0] - .5) * 2
            q = d * k
            if cut is not None and q[1] < -cut:
                q[1] = -cut
            P[j, i] = (c[0] + q[0] * r * squash[0], c[1] + q[1] * r * squash[1], c[2] + q[2] * r * squash[2])
            nn = np.array([d[0] / squash[0], d[1] / squash[1], d[2] / squash[2]])
            if cut is not None and q[1] <= -cut + 1e-9 and j > nv * .6:
                nn = np.array([0, -1, 0])
            N[j, i] = nn / (np.linalg.norm(nn) or 1)
    for j in range(nv):
        for i in range(nu):
            i1 = (i + 1) % nu
            pts = [tuple(P[j, i]), tuple(P[j, i1]), tuple(P[j + 1, i1]), tuple(P[j + 1, i])]
            nrm = [tuple(N[j, i]), tuple(N[j, i1]), tuple(N[j + 1, i1]), tuple(N[j + 1, i])]
            ny = (N[j, i][1] + N[j + 1, i][1] + N[j, i1][1] + N[j + 1, i1][1]) / 4
            f = fins[0] if ny > .35 else (fins[2] if ny < -.3 else fins[1])
            if j == 0:
                g.face(pts[1:], f, normals=nrm[1:])
            elif j == nv - 1:
                g.face(pts[:3], f, normals=nrm[:3])
            else:
                g.face(pts, f, normals=nrm)


def leafy(g, b, T, seed, fins, depth=.12, cell=None, faces=('+x', '-x', '+z', '-z', '+y'), flowers=None):
    """A clipped mass of leaves filling the box b: each face a fine grid swelling in and out by up to `depth` (never past b grown
    by `depth`), shaded round (the normals follow the swell), its greens in drifts from `fins` and, sparsely, `flowers`."""
    x0, x1, y0, y1, z0, z1 = b
    c = cell or (.2, .45, 1.2)[T.i]
    def face(o, u, v, n):
        lu = math.sqrt(u[0] ** 2 + u[1] ** 2 + u[2] ** 2)
        lv = math.sqrt(v[0] ** 2 + v[1] ** 2 + v[2] ** 2)
        cu, cv = max(1, int(round(lu / c))), max(1, int(round(lv / c)))
        uu, vv = (u[0] / lu, u[1] / lu, u[2] / lu), (v[0] / lv, v[1] / lv, v[2] / lv)
        base = np.array([[o[0] + u[0] * i / cu + v[0] * j / cv, o[1] + u[1] * i / cu + v[1] * j / cv, o[2] + u[2] * i / cu + v[2] * j / cv] for i in range(cu + 1) for j in range(cv + 1)])
        d = (sk.fbm(base + seed * .37, 2.6, 3, seed % 89) - .35) * depth * 2.0
        d = np.clip(d, -depth * .6, depth).reshape(cu + 1, cv + 1)
        d[0, :] = d[-1, :] = 0
        d[:, 0] = d[:, -1] = 0
        tone = sk.fbm(base * 1.0 + seed * .11, .45, 2, (seed + 7) % 89).reshape(cu + 1, cv + 1)
        base = base.reshape(cu + 1, cv + 1, 3)
        pts, nrm = {}, {}
        for i in range(cu + 1):
            for j in range(cv + 1):
                p = base[i, j]
                pts[(i, j)] = (p[0] + n[0] * d[i, j], p[1] + n[1] * d[i, j], p[2] + n[2] * d[i, j])
                du = (d[min(i + 1, cu), j] - d[max(i - 1, 0), j]) / (lu / cu * (min(i + 1, cu) - max(i - 1, 0)) or 1)
                dv = (d[i, min(j + 1, cv)] - d[i, max(j - 1, 0)]) / (lv / cv * (min(j + 1, cv) - max(j - 1, 0)) or 1)
                q = (n[0] - uu[0] * du * 1.4 - vv[0] * dv * 1.4, n[1] - uu[1] * du * 1.4 - vv[1] * dv * 1.4, n[2] - uu[2] * du * 1.4 - vv[2] * dv * 1.4)
                l = math.sqrt(q[0] ** 2 + q[1] ** 2 + q[2] ** 2)
                nrm[(i, j)] = (q[0] / l, q[1] / l, q[2] / l)
        for i in range(cu):
            for j in range(cv):
                t = (tone[i, j] + tone[i + 1, j] + tone[i, j + 1] + tone[i + 1, j + 1]) / 4 + (d[i, j] + d[i + 1, j + 1]) / depth * .04
                f = fins[0] if t < .43 else (fins[-1] if t > .6 else fins[1])
                if flowers and hashf(seed + 9, i * 3, j * 5, n[0] + 2 * n[2]) > .955:
                    f = flowers[int(hashf(seed, i, j) * 97) % len(flowers)]
                ks = ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1))
                g.face([pts[k] for k in ks], f, normals=[nrm[k] for k in ks])
    if '-z' in faces:
        face((x0, y0, z0), (x1 - x0, 0, 0), (0, y1 - y0, 0), (0, 0, -1))
    if '+z' in faces:
        face((x0, y0, z1), (x1 - x0, 0, 0), (0, y1 - y0, 0), (0, 0, 1))
    if '-x' in faces:
        face((x0, y0, z0), (0, 0, z1 - z0), (0, y1 - y0, 0), (-1, 0, 0))
    if '+x' in faces:
        face((x1, y0, z0), (0, 0, z1 - z0), (0, y1 - y0, 0), (1, 0, 0))
    if '+y' in faces:
        face((x0, y1, z0), (x1 - x0, 0, 0), (0, 0, z1 - z0), (0, 1, 0))


def bed(g, x0, x1, y, z0, z1, T, seed, fins, flowers):
    """A planting bed set in a floor: a stone edging, soil, low tufts and flowers (never a hand over the floor: nothing to
    stand beside, ankle high)."""
    rect(g, x0 - .06, x1 + .06, y, z0 - .06, z1 + .06, F['coping'], 0, 1)
    if T.i == 2:
        rect(g, x0, x1, y, z0, z1, fins[1], 0, 2)
        return
    rect(g, x0, x1, y, z0, z1, F['soil'], 0, 2)
    n = int((x1 - x0) * (z1 - z0) * (7 if T.fine else 3)) + 1
    for k in range(n):
        px = x0 + .1 + hashf(seed, k) * (x1 - x0 - .2)
        pz = z0 + .1 + hashf(seed, k, 1) * (z1 - z0 - .2)
        r = .1 + .09 * hashf(seed, k, 2)
        fl = hashf(seed, k, 3)
        cols = (flowers[k % len(flowers)], fins[1], fins[0]) if fl > .72 else (fins[2], fins[1], fins[0])
        blob(g, (px, y + ly(2) + r * .25, pz), r, T, seed + k, cols, squash=(1, .8, 1), cut=.35, lumps=.3, sides=(6, 5, 4)[T.i])


def hsection(g, cx, cz, y0, y1, T, f, d=.5, w=.5, turn=False):
    """A steel H column standing at (cx, cz): two flanges and a web (`turn`: the flanges across z)."""
    tf, tw = .045, .035
    if turn:
        box(g, cx - w / 2, cx + w / 2, y0, y1, cz - d / 2, cz - d / 2 + tf, f, None, 0, cell=0)
        box(g, cx - w / 2, cx + w / 2, y0, y1, cz + d / 2 - tf, cz + d / 2, f, None, 0, cell=0)
        box(g, cx - tw / 2, cx + tw / 2, y0, y1, cz - d / 2 + tf, cz + d / 2 - tf, f, None, 0, cell=0, skip=('+z', '-z'))
    else:
        box(g, cx - d / 2, cx - d / 2 + tf, y0, y1, cz - w / 2, cz + w / 2, f, None, 0, cell=0)
        box(g, cx + d / 2 - tf, cx + d / 2, y0, y1, cz - w / 2, cz + w / 2, f, None, 0, cell=0)
        box(g, cx - d / 2 + tf, cx + d / 2 - tf, y0, y1, cz - tw / 2, cz + tw / 2, f, None, 0, cell=0, skip=('+x', '-x'))


def tint(g, f_from, pick):
    """Re-finishes the faces of finish f_from by pick(face centre) -> finish (clouds by height, leaves by light)."""
    P, N, S, Fi = g.arrays()
    starts = np.zeros(len(S), dtype=np.int64)
    if len(S) > 1:
        starts[1:] = np.cumsum(S[:-1])
    c = np.add.reduceat(P.astype(np.float64), starts, axis=0) / S[:, None]
    for i in np.nonzero(Fi == f_from)[0]:
        g.fin[i] = pick(c[i])
