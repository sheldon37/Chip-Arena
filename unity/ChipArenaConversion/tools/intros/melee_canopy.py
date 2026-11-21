"""Apex's set (the Fangs; docs/INTROS-MELEE.md): the Canopy, overgrown jungle ruins at night.

A broken temple gateway stands behind the pilot: two square pillars (x = +-1.9, z = -3.0) carry a massive carved lintel whose flat
top (y 3.2, 0.7 m deep between its worn edges) the pilot crouches on before the pounce: a predator's mask and glyph panels on its
front, a strangler fig's roots gripping its right (-x) end, its left (+x) end broken off and lying in the court. Broken flagstones
under leaf litter and moss fill the court in front, heaved by roots, a few puddles in the sunken places; a toppled serpent head
lies at the right, a stepped altar with a broken stele stands at the left; broken walls run off either side. Through the gate a
broken path leads to a stair with serpent balustrades and a ruined shrine. Huge buttressed trunks, lianas and hanging vines, fern
fans and elephant ears close every side (the camera's side too), and a ceiling of dark leaf masses at 8-12 m closes the sky. A few
bracket fungi glow faintly (glow finish; the intro can scale it). No rigs. Set frame: melee_kit's (the pilot at the origin facing
+z; -x is the opening camera's right).
"""
import array
import math

import numpy as np

import melee_kit as mk
from melee_kit import sk

# ---- finishes (albedo kept mid-dark so the night's coloured light reads on it) ----
F_STONE = mk.fin('canopy.stone', 0x7a7661, sk.DETAIL_CARVED, smooth=.3, dirt=1.0, moss=1.2, wear=.45)
F_STONE_DK = mk.fin('canopy.stone.dark', 0x64624f, sk.DETAIL_CARVED, smooth=.3, dirt=1.1, moss=1.4, wear=.35)
F_RELIEF = mk.fin('canopy.relief', 0x8a856c, sk.DETAIL_CARVED, smooth=.35, dirt=1.0, moss=.9, wear=.6)
F_CRACK = mk.fin('canopy.crack', 0x36322b, smooth=.2, dirt=.2)
F_FLAG = mk.fin('canopy.flag', 0x6e6c5b, sk.DETAIL_CARVED, smooth=.4, dirt=1.0, moss=1.1, wear=.35)
F_FLAG_DK = mk.fin('canopy.flag.dark', 0x5c5b4e, sk.DETAIL_CARVED, smooth=.4, dirt=1.1, moss=1.3, wear=.3)
F_FLAG_MOSS = mk.fin('canopy.flag.moss', 0x5b6447, sk.DETAIL_CARVED, smooth=.3, dirt=1.0, moss=1.5, wear=.2)
F_EARTH = mk.fin('canopy.earth', 0x4d3f2e, smooth=.15, dirt=.8, moss=.9)
F_MUD = mk.fin('canopy.mud', 0x3c3328, smooth=.55, dirt=.5)
F_WATER = mk.fin('canopy.water', 0x182120, smooth=.97, dirt=0)
F_MOSS = mk.fin('canopy.moss', 0x4a622f, smooth=.15, dirt=.5)
F_MOSS_LT = mk.fin('canopy.moss.light', 0x617838, smooth=.15, dirt=.4)
F_BARK = mk.fin('canopy.bark', 0x5e5645, sk.DETAIL_WOOD, smooth=.25, dirt=.9, moss=1.0, param=1)
F_BARK_PALE = mk.fin('canopy.bark.pale', 0x7c7764, sk.DETAIL_WOOD, smooth=.25, dirt=1.0, moss=.9, param=1)
F_ROOT = mk.fin('canopy.root', 0x6b614d, sk.DETAIL_CARVED, smooth=.3, dirt=.9, moss=.8, wear=.2)
F_ROOT_PALE = mk.fin('canopy.root.pale', 0x7a745f, sk.DETAIL_CARVED, smooth=.3, dirt=.9, moss=.7, wear=.25)
F_LOG = mk.fin('canopy.log', 0x5a4a37, sk.DETAIL_WOOD, smooth=.25, dirt=.9, moss=1.3, param=0)
F_HEART = mk.fin('canopy.log.heart', 0x7a5e3f, sk.DETAIL_WOOD, smooth=.2, dirt=.6, param=1)
F_VINE = mk.fin('canopy.vine', 0x55522f, smooth=.3, dirt=.5)
F_LIANA = mk.fin('canopy.liana', 0x5d553c, sk.DETAIL_WOOD, smooth=.3, dirt=.6, param=1)
F_LEAF = mk.fin('canopy.leaf', 0x3e602c, smooth=.45, dirt=.2)
F_LEAF_DK = mk.fin('canopy.leaf.dark', 0x324c2a, smooth=.4, dirt=.2)
F_LEAF_LT = mk.fin('canopy.leaf.light', 0x5b7f39, smooth=.45, dirt=.2)
F_LEAF_Y = mk.fin('canopy.leaf.yellow', 0x7d7f3b, smooth=.4, dirt=.25)
F_FERN = mk.fin('canopy.fern', 0x456d31, smooth=.35, dirt=.2)
F_FERN_DK = mk.fin('canopy.fern.dark', 0x385a2c, smooth=.35, dirt=.2)
F_LITTER = mk.fin('canopy.litter', 0x705030, smooth=.25, dirt=.4)
F_LITTER_OC = mk.fin('canopy.litter.ochre', 0x8c6c39, smooth=.25, dirt=.4)
F_LITTER_DK = mk.fin('canopy.litter.dark', 0x523f29, smooth=.25, dirt=.4)
F_CROWN = mk.fin('canopy.crown', 0x34492c, smooth=.3, dirt=.3)
F_CROWN_DK = mk.fin('canopy.crown.dark', 0x30402e, smooth=.3, dirt=.3)
F_FAR = mk.fin('canopy.far', 0x313b31, smooth=.2, dirt=0)
F_HOLLOW = mk.fin('canopy.hollow', 0x302c27, smooth=.1, dirt=0)
F_FUNGUS = mk.fin('canopy.fungus.glow', 0x9effc4, glow=1.4)

PX = 1.9            # the pillars' centres (x = +-PX)
GZ = -3.0           # the gateway's line
TOP = 3.2           # the lintel's walkable top
PH = .45            # the pillars' half width
CAP_Y = 2.12        # the capitals' foot
L_LO = 2.36         # the lintel's underside
L_X0, L_X1 = -2.8, 2.6   # the lintel's ends (-x gripped by the fig, +x broken off)
L_HZ = .425         # the lintel's half depth (0.85 m; its flat top about 0.7 m between the worn edges)
WALL_X0, WALL_X1 = 2.62, 7.6
PATH_HALF = 1.7     # the broken path through the gate
STAIR_Z = -8.4      # the stair's first riser
STAIR_N, STAIR_RISE, STAIR_RUN = 5, .32, .55
STAIR_HALF = 2.3
PLAT_Y = STAIR_N * STAIR_RISE
PLAT_Z = STAIR_Z - STAIR_N * STAIR_RUN
SHRINE_Z = -12.3

# trees: (x, z, core radius, height, fins, fin reach, seed, bark)
TREES = (
    (-3.95, -5.05, .95, 13.5, 6, 1.75, 1, F_BARK_PALE),    # the strangler fig behind the right pillar
    (5.3, -5.5, .85, 13.0, 5, 1.6, 2, F_BARK),
    (-7.0, 1.2, .75, 12.0, 4, 1.35, 3, F_BARK),
    (8.6, 2.8, .7, 12.0, 4, 1.25, 4, F_BARK_PALE),
    (-4.5, 8.2, .85, 12.5, 5, 1.5, 5, F_BARK),             # the camera's side
    (4.7, 9.0, .95, 13.0, 5, 1.6, 6, F_BARK_PALE),
    (.4, 13.2, .7, 12.0, 3, 1.2, 7, F_BARK),
)
PUDDLES = ((-1.55, 1.85, .62, .42, .4), (2.15, -1.4, .48, .36, -.5), (2.8, 4.3, .75, .5, .2))   # (x, z, rx, rz, turn)


def build(T):
    s = mk.SetPiece('canopy')
    g = mk.Geo()
    tl = mk.Tally(g)
    rec = []
    ground(g, T)
    tl('ground')
    flags(g, T, rec)
    puddles(g, T)
    tl('flags')
    litter(g, T, rec)
    tl('litter')
    gateway(g, T)
    tl('gateway')
    walls(g, T)
    tl('walls')
    stair(g, T)
    tl('stair+shrine')
    serpent(g, T)
    altar(g, T)
    tl('serpent+altar')
    trees(g, T)
    tl('trees')
    fig_roots(g, T)
    surface_roots(g, T)
    tl('roots')
    vines(g, T)
    tl('vines')
    plants(g, T)
    tl('plants')
    crown(g, T)
    tl('crown')
    far(g, T)
    tl('far')
    s.tally = tl
    s.static[T.i] = g
    s.views = [
        ('front', (0.0, 1.4, 6.0), (0.0, 1.8, -3.0), 50),
        ('worm', (0.5, 0.3, 1.5), (0.0, 3.0, -3.0), 62),
        ('lintel', (0.25, 4.75, -3.55), (0.0, 0.0, 0.9), 58),
        ('side', (6.0, 2.0, 0.0), (0.0, 1.5, -2.0), 52),
        ('gate', (0.4, 1.3, 2.4), (0.0, 1.5, -10.0), 55),
        ('reverse', (0.6, 2.4, -7.6), (0.0, 1.4, 4.0), 55),
        ('up', (0.4, 1.2, 1.0), (0.0, 11.0, -2.5), 75),
        ('high', (6.5, 8.0, 8.5), (0.0, 0.5, -2.5), 55),
    ]
    return s


# =============================================================================================================================
# Shared shaping: indexed meshes into the corner Geo, weathered blocks, rough boxes, tapered limbs, two-sided leaves
# =============================================================================================================================
def qmul(a, b):
    ax, ay, az, aw = a
    bx, by, bz, bw = b
    return (aw * bx + ax * bw + ay * bz - az * by, aw * by - ax * bz + ay * bw + az * bx,
            aw * bz + ax * by - ay * bx + az * bw, aw * bw - ax * bx - ay * by - az * bz)


def qe(yaw=0.0, pitch=0.0, roll=0.0):
    """Turn about y (yaw), then x (pitch), then z (roll), applied roll first."""
    return qmul(qmul(sk.quat_axis(0, 1, 0, yaw), sk.quat_axis(1, 0, 0, pitch)), sk.quat_axis(0, 0, 1, roll))


def rotm(q):
    return np.array(sk.affine(q), dtype=np.float64).reshape(3, 4)[:, :3]


def _t(p):
    return (float(p[0]), float(p[1]), float(p[2]))


def ss(e0, e1, x):
    t = max(0.0, min(1.0, (x - e0) / (e1 - e0)))
    return t * t * (3 - 2 * t)


def emit(g, V, faces, f, smooth=True, orient='volume', crease=None, prefer=None):
    """An indexed mesh (V points, faces as index tuples of one consistent winding) into the Geo with one finish. orient: 'volume'
    turns the whole mesh outward (closed or nearly closed shapes), a vector turns it toward that side, None leaves it. smooth:
    vertex normals (area weighted); crease (a cosine): each corner only averages the faces turned less than that from its own.
    prefer (normals per point, weight): the vertex normals pulled toward these (a block's flat faces and round edges)."""
    V = np.asarray(V, dtype=np.float64)
    if not len(faces):
        return
    FN = np.zeros((len(faces), 3))
    groups = {}
    for i, fc in enumerate(faces):
        groups.setdefault(len(fc), []).append(i)
    for k, ids in groups.items():
        P = V[np.array([faces[i] for i in ids], dtype=np.int64)]
        acc = np.zeros((len(ids), 3))
        for i in range(1, k - 1):
            acc += np.cross(P[:, i] - P[:, 0], P[:, i + 1] - P[:, 0])
        FN[ids] = acc
    flip = False
    if isinstance(orient, str):
        used = np.unique(np.concatenate([np.asarray(fc) for fc in faces]))
        c = V[used].mean(axis=0)
        first = V[np.array([fc[0] for fc in faces])]
        flip = float(np.sum((first - c) * FN)) < 0
    elif orient is not None:
        flip = float(np.sum(FN @ np.asarray(orient, dtype=np.float64))) < 0
    if flip:
        faces = [tuple(fc[::-1]) for fc in faces]
        FN = -FN
    U = FN / np.maximum(np.linalg.norm(FN, axis=1, keepdims=True), 1e-12)
    if smooth:
        VN = np.zeros_like(V)
        for i, fc in enumerate(faces):
            for v in fc:
                VN[v] += FN[i]
        VNn = VN / np.maximum(np.linalg.norm(VN, axis=1, keepdims=True), 1e-12)
        if prefer is not None:
            Np, w = prefer
            if flip:
                Np = -Np
            VNn = VNn * (1 - w) + Np * w
            VNn /= np.maximum(np.linalg.norm(VNn, axis=1, keepdims=True), 1e-12)
        if crease is not None:
            vf = {}
            for i, fc in enumerate(faces):
                for v in fc:
                    vf.setdefault(v, []).append(i)
    for i, fc in enumerate(faces):
        pts = [_t(V[v]) for v in fc]
        if not smooth:
            g.face(pts, f, normal=_t(U[i]))
            continue
        if crease is None:
            g.face(pts, f, normals=[_t(VNn[v]) for v in fc])
            continue
        ns = []
        for v in fc:
            sel = [j for j in vf[v] if float(U[i] @ U[j]) >= crease]
            a = FN[sel].sum(axis=0)
            ns.append(_t(a / max(float(np.linalg.norm(a)), 1e-12)))
        g.face(pts, f, normals=ns)


def block(g, T, c, h, f, seed=0, b=.05, rough=.016, chip=.08, cell=.3, q=None, skip=(), flat_top=False, brk=None, lo=False):
    """A weathered stone block (half extents h) centred at c, turned by q: its edges rounded by b, its faces cut into cells of
    about `cell` and pushed in and out by `rough`, its edges and corners bitten by chips up to `chip` deep. brk {'+x': depth}:
    that end a rough break. flat_top: the top face kept nearly flat (walkable). Low tier (or lo): a rough box."""
    q = q or (0.0, 0.0, 0.0, 1.0)
    if T.i == 2 or lo:
        rbox(g, T, c, h, f, seed=seed, b=min(b, .05), jit=min(.03, rough * 1.5), chop=chip if not flat_top else 0.0, q=q,
             skip=skip)
        return
    axes, bb = [], []
    for hh in h:
        b_ = min(b, hh * .3)
        n = max(1, int(math.ceil((2 * hh - 2 * b_) / cell)))
        axes.append([-hh] + [float(v) for v in np.linspace(-hh + b_, hh - b_, n + 1)] + [hh])
        bb.append(b_)
    dims = [len(a) for a in axes]
    idx = {}
    P = []
    for i in range(dims[0]):
        for j in range(dims[1]):
            for k in range(dims[2]):
                if 0 < i < dims[0] - 1 and 0 < j < dims[1] - 1 and 0 < k < dims[2] - 1:
                    continue
                idx[(i, j, k)] = len(P)
                P.append((axes[0][i], axes[1][j], axes[2][k]))
    P = np.array(P)
    H = np.array(h, dtype=np.float64)
    B = np.array(bb)
    inner = H - B
    Q = np.clip(P, -inner, inner)
    E = (P - Q) / B
    En = E / np.maximum(np.linalg.norm(E, axis=1, keepdims=True), 1e-9)
    base = Q + En * B
    N = En / B
    N /= np.maximum(np.linalg.norm(N, axis=1, keepdims=True), 1e-9)
    W = base + np.array([seed * 1.37, seed * 2.11, seed * .73])
    amp = np.full(len(P), rough * 2)
    if flat_top:
        amp = np.where(N[:, 1] > .85, rough * .3, amp)
    disp = (sk.fbm(W * 2.2, 1.0, 3, seed) - .5) * amp
    # chips: bites out of the edges where a coarse noise says so, and the corners knocked off more often
    ed = np.sort(H - np.abs(P), axis=1)
    cw = max(cell, .18) * 1.15
    cm = sk.smooth(.42, .6, sk.fbm(W * 1.6 + 5.0, 1.0, 2, seed + 7))
    bite = chip * cm * (1 - sk.smooth(0.0, cw, ed[:, 1]))
    cc = sk.smooth(.36, .56, sk.fbm(W * 1.1 + 9.0, 1.0, 2, seed + 9))
    bite = bite + chip * .8 * cc * (1 - sk.smooth(0.0, cw * 1.3, ed[:, 2]))
    if flat_top:
        bite = np.where(N[:, 1] > .85, bite * .4, bite)
    disp -= bite
    if brk:
        nb = sk.fbm(W * 3.0 + 11.0, 1.0, 3, seed + 13)
        for key, depth in brk.items():
            a = 'xyz'.index(key[1])
            sg = 1.0 if key[0] == '+' else -1.0
            span = max(depth * 1.6, cell * 1.3)
            t = sk.smooth(H[a] - span, H[a], P[:, a] * sg)
            base[:, a] -= sg * depth * (.15 + .85 * nb) * t
            disp -= .6 * chip * t
    V = base + N * disp[:, None]
    R = rotm(q)
    V = V @ R.T + np.array(c, dtype=np.float64)
    Nw = N @ R.T
    faces = []
    for a in range(3):
        u, v = (a + 1) % 3, (a + 2) % 3
        for side in (0, dims[a] - 1):
            key = ('+' if side else '-') + 'xyz'[a]
            if key in skip:
                continue
            for iu in range(dims[u] - 1):
                for iv in range(dims[v] - 1):
                    quad = []
                    for du, dv in ((0, 0), (1, 0), (1, 1), (0, 1)):
                        ii = [0, 0, 0]
                        ii[a], ii[u], ii[v] = side, iu + du, iv + dv
                        quad.append(idx[tuple(ii)])
                    faces.append(tuple(quad) if side else tuple(quad[::-1]))
    emit(g, V, faces, f, smooth=True, orient='volume', prefer=(Nw, .65))


def rbox(g, T, c, h, f, seed=0, b=.04, jit=.025, chop=0.0, q=None, skip=()):
    """A cheap rough stone (24 vertices): a bevelled box whose eight corners are each pushed out of true by up to `jit`, one of
    them knocked in by `chop`; its faces' normals follow the new corners."""
    r = mk.rng(seed + 7919)
    t = mk.Geo()
    sk.box(t, -h[0], h[0], -h[1], h[1], -h[2], h[2], f, T, bevel=b, cell=0, skip=skip)
    P = np.frombuffer(t.p, dtype=np.float32).reshape(-1, 3).astype(np.float64)
    N = np.frombuffer(t.n, dtype=np.float32).reshape(-1, 3).astype(np.float64)
    bits = (P > 0).astype(np.int64)
    key = bits[:, 0] * 4 + bits[:, 1] * 2 + bits[:, 2]
    J = r.uniform(-jit, jit, (8, 3))
    if chop > 0:
        k = int(r.integers(0, 8))
        d = np.array([(k >> 2) & 1, (k >> 1) & 1, k & 1], dtype=np.float64) * 2 - 1
        J[k] -= d * chop * .6
    P2 = P + J[key]
    hv = np.array(h, dtype=np.float64)
    newn = {}
    for a in range(3):
        u, v = (a + 1) % 3, (a + 2) % 3
        for s_ in (0, 1):
            cs = []
            for du, dv in ((0, 0), (1, 0), (1, 1), (0, 1)):
                bb = [0, 0, 0]
                bb[a], bb[u], bb[v] = s_, du, dv
                kk = bb[0] * 4 + bb[1] * 2 + bb[2]
                cs.append((np.array(bb) * 2 - 1) * hv + J[kk])
            n = np.cross(cs[2] - cs[0], cs[3] - cs[1])
            if n[a] * (1 if s_ else -1) < 0:
                n = -n
            newn[(a, s_)] = n / max(float(np.linalg.norm(n)), 1e-12)
    N2 = N.copy()
    for a in range(3):
        for s_ in (0, 1):
            sel = np.abs(N[:, a] - (1.0 if s_ else -1.0)) < 1e-4
            N2[sel] = newn[(a, s_)]
    t.p = array.array('f')
    t.p.frombytes(P2.astype(np.float32).tobytes())
    t.n = array.array('f')
    t.n.frombytes(N2.astype(np.float32).tobytes())
    g.extend(t.transformed(sk.affine(q or (0.0, 0.0, 0.0, 1.0), c)))


def limb(g, T, pts, r0, r1, f, sides=None, per=None, flat=1.0, caps=False, up=None, ease=1.0):
    """A tapered tube (a root, a limb, a liana) along a Catmull-Rom curve through pts, radius r0 to r1; flat < 1 squashes it
    (a root pressed to the ground or a stone)."""
    sides = sides or (6, 5, 4)[T.i]
    per = per or (4, 3, 2)[T.i]
    path = sk.catmull(pts, per)
    n = len(path)
    if n < 2:
        return
    sc = [r0 + (r1 - r0) * (i / (n - 1)) ** ease for i in range(n)]
    sec = [(math.cos(2 * math.pi * k / sides), math.sin(2 * math.pi * k / sides) * flat) for k in range(sides)]
    sk.sweep(g, sec, path, f, caps=caps, up=up, scale=sc)


def sheet(g, G, f, up=(0.0, 1.0, 0.0), fb=None):
    """A two-sided surface over a grid of points G (nv rows by nu columns), smooth shaded; its front side turned toward `up`."""
    G = np.asarray(G, dtype=np.float64)
    du = np.gradient(G, axis=1)
    dv = np.gradient(G, axis=0)
    N = np.cross(du, dv)
    N /= np.maximum(np.linalg.norm(N, axis=2, keepdims=True), 1e-12)
    if float(np.sum(N.reshape(-1, 3) @ np.asarray(up))) < 0:
        N = -N
    fb = f if fb is None else fb
    nv, nu = G.shape[:2]
    for j in range(nv - 1):
        for i in range(nu - 1):
            ids = ((j, i), (j, i + 1), (j + 1, i + 1), (j + 1, i))
            pts = [_t(G[a]) for a in ids]
            ns = [_t(N[a]) for a in ids]
            g.face(pts, f, normals=ns)
            g.face(pts[::-1], fb, normals=[(-n[0], -n[1], -n[2]) for n in ns[::-1]])


def leaf_card(g, base, d, L, W, f, fb=None, fold=.25):
    """A small leaf: a two-sided diamond folded along its midrib (eight corners)."""
    d = sk._norm(d)
    side = sk._norm(sk._cross(d, (0, 1, 0))) if abs(d[1]) < .95 else (1.0, 0.0, 0.0)
    up = sk._norm(sk._cross(side, d))
    if up[1] < 0:
        up = (-up[0], -up[1], -up[2])
    tip = sk._add(base, d, L)
    mid = sk._add(base, d, L * .45)
    a1 = sk._add(sk._add(mid, side, W * .5), up, -W * fold)
    a2 = sk._add(sk._add(mid, side, -W * .5), up, -W * fold)
    g.face((base, a1, tip, a2), f, normal=up)
    g.face((a2, tip, a1, base), fb if fb is not None else f, normal=(-up[0], -up[1], -up[2]))


def moss_pad(g, T, c, rr, f, seed, n=None, droop=.04, rz=None):
    """A cushion of moss lying on a top: a low dome with a ragged rim."""
    r = mk.rng(seed)
    n = n or (9, 7, 5)[T.i]
    x, y, z = c
    rz = rr if rz is None else rz
    V = [(x, y + .03, z)]
    for k in range(n):
        a = 2 * math.pi * k / n + float(r.uniform(-.2, .2))
        w = float(r.uniform(.6, 1.15))
        V.append((x + math.cos(a) * rr * w, y - droop * float(r.uniform(.3, 1.0)), z + math.sin(a) * rz * w))
    faces = [(0, 1 + i, 1 + (i + 1) % n) for i in range(n)]
    emit(g, V, faces, f, smooth=True, orient=(0, 1, 0))


def strand(g, T, top, length, w, f, seed):
    """A beard of moss hanging from an edge: a two-sided ribbon, swaying a little."""
    r = mk.rng(seed)
    n = (3, 2, 1)[T.i]
    a = float(r.uniform(0, math.pi))
    d = (math.cos(a) * w, 0, math.sin(a) * w)
    G = []
    for i in range(n + 1):
        t = i / n
        c = (top[0] + float(r.uniform(-.04, .04)) * t, top[1] - length * t, top[2] + float(r.uniform(-.04, .04)) * t)
        ww = 1 - .75 * t
        G.append([sk._add(c, d, -ww), sk._add(c, d, ww)])
    sheet(g, G, f, up=(math.sin(a), 0, -math.cos(a)))


def spiral(g, T, c, r0, turns, n, f, hand=1, sec=.03):
    """A carved spiral scroll on a face (normal n) at c: radius r0 winding in `turns` times."""
    n = sk._norm(n)
    u = sk._norm(sk._cross((0, 1, 0), n)) if abs(n[1]) < .9 else (1, 0, 0)
    v = sk._cross(n, u)
    k = max(4, int((16, 9, 6)[T.i] * turns / 2))
    pts = []
    for i in range(k + 1):
        t = i / k
        a = hand * t * turns * 2 * math.pi
        rr = r0 * (1 - .82 * t)
        pts.append(sk._add(sk._add(c, u, math.cos(a) * rr), v, math.sin(a) * rr))
    sk.sweep(g, sk.rect_section(sec, sec * .8), pts, f, up=n)


def hull(pts):
    """The convex hull of 2D points (counter-clockwise)."""
    p = sorted(set((round(a, 5), round(b, 5)) for a, b in pts))
    if len(p) < 3:
        return p

    def cr(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    lo, hi = [], []
    for q in p:
        while len(lo) >= 2 and cr(lo[-2], lo[-1], q) <= 0:
            lo.pop()
        lo.append(q)
    for q in reversed(p):
        while len(hi) >= 2 and cr(hi[-2], hi[-1], q) <= 0:
            hi.pop()
        hi.append(q)
    return lo[:-1] + hi[:-1]


def blob2d(cx, cz, rx, rz, n, r, turn=0.0, wob=.12):
    pts = []
    for k in range(n):
        a = 2 * math.pi * k / n
        w = 1 + float(r.uniform(-wob, wob))
        x, z = math.cos(a) * rx * w, math.sin(a) * rz * w
        pts.append((cx + x * math.cos(turn) - z * math.sin(turn), cz + x * math.sin(turn) + z * math.cos(turn)))
    return hull(pts)


def in_poly(x, z, poly):
    ins = False
    n = len(poly)
    for i in range(n):
        a, b = poly[i], poly[(i + 1) % n]
        if (a[1] > z) != (b[1] > z):
            t = (z - a[1]) / (b[1] - a[1])
            if x < a[0] + t * (b[0] - a[0]):
                ins = not ins
    return ins


# =============================================================================================================================
# The ground: the jungle floor, the paved court and path, flagstones, puddles and litter
# =============================================================================================================================
def paved(x, z):
    """How far inside the paving a point is (m; negative outside): the court in front of the gate and the path behind it, their
    edges ragged."""
    court = min(5.6 - abs(x), z + 3.5, 6.4 - z)
    path = min(PATH_HALF - abs(x), z - STAIR_Z + .1, -3.2 - z)
    wob = .7 * math.sin(x * 1.3 + z * .7) + .45 * math.sin(z * 2.1 - x * .4)
    return max(court, path) + wob * .5


def ground_y(x, z):
    """The earth's height: just under the flags in the paving, rising in lumps outside it, banked against the walls, mounded
    round the trees, climbing behind the stair to the platform's level."""
    out = 1 - ss(-.2, .9, paved(x, z))
    n = math.sin(x * .9 + 1.3) * math.cos(z * .7 - .4) * .5 + math.sin(x * 2.3 - z * 1.7) * .25
    y = -.05 + out * (.06 + .14 * (n + .75))
    y += ss(2.4, 3.1, abs(x)) * math.exp(-((z - GZ) / 1.0) ** 2) * .3
    r = math.hypot(x, z)
    y += .022 * max(0.0, r - 9.0) ** 1.35
    for tx, tz, rr, *_ in TREES:
        d2 = (x - tx) ** 2 + (z - tz) ** 2
        y += .32 * math.exp(-d2 / (rr * 2.4) ** 2)
    k = ss(STAIR_Z + .4, PLAT_Z - .5, z) * (1 - .35 * ss(4.0, 7.0, abs(x)))
    return y + (PLAT_Y - .15 + .15 * n - y) * k


def axis_samples(T):
    near = (1.0, 1.4, 1.8)[T.i]
    xs = list(np.arange(-10.0, 10.0 + 1e-6, near))
    mid = [-24.0, -18.0, -14.0, -12.0, 12.0, 14.0, 18.0, 24.0] if T.i < 2 else [-24.0, -16.0, 16.0, 24.0]
    return sorted(set([float(v) for v in xs] + mid))


def ground(g, T):
    xs = axis_samples(T)
    zs = axis_samples(T)
    V = [(x, ground_y(x, z), z) for z in zs for x in xs]
    nu = len(xs)
    faces = []
    for j in range(len(zs) - 1):
        for i in range(nu - 1):
            faces.append((j * nu + i, j * nu + i + 1, (j + 1) * nu + i + 1, (j + 1) * nu + i))
    emit(g, V, faces, F_EARTH, smooth=True, orient=(0, 1, 0))


# the roots that crawl over the court (control points x, y, z; radius at the tree, at the tip): the flags heave over them
SURFACE_ROOTS = (
    (((-2.9, -.02, -1.6), (-2.75, .06, -1.0), (-3.05, .04, -.3), (-3.6, .07, .3), (-4.3, -.04, .6)), .08, .035),
    (((-2.6, -.02, -2.2), (-2.25, .05, -1.95), (-1.8, .02, -1.98), (-1.6, -.04, -2.3)), .055, .03),
    (((-5.9, .1, .8), (-5.0, .06, .5), (-4.2, .05, .9), (-3.3, .04, 1.6), (-2.9, -.03, 2.1)), .13, .04),
    (((-6.2, .08, 2.0), (-5.4, .04, 2.6), (-4.6, .05, 2.5), (-4.0, -.03, 3.1)), .1, .04),
    (((4.2, .1, -4.6), (3.6, .06, -3.9), (3.15, .06, -3.3), (2.75, .05, -2.75), (2.8, -.03, -2.3)), .11, .04),
    (((-3.7, .08, 7.1), (-3.0, .05, 6.3), (-2.0, .06, 5.9), (-1.3, .04, 5.2), (-1.0, -.03, 4.6)), .11, .04),
    (((3.9, .08, 7.9), (3.3, .05, 6.9), (3.4, .04, 6.0), (2.9, -.03, 5.3)), .11, .04),
    (((7.6, .1, 2.6), (6.4, .06, 2.0), (5.5, .05, 2.3), (4.8, -.03, 1.7)), .11, .04),
    (((-3.2, .08, -5.6), (-2.3, .05, -5.9), (-1.4, .06, -6.4), (-.4, .04, -6.6), (.5, -.03, -7.2)), .1, .035),
    (((4.5, .08, -6.2), (3.4, .05, -6.6), (2.2, .06, -6.9), (1.3, -.03, -7.6)), .1, .035),
)


def dist_to_lines(x, z, lines):
    best = 99.0
    for ln in lines:
        for (ax, az), (bx, bz) in zip(ln[:-1], ln[1:]):
            vx, vz = bx - ax, bz - az
            L2 = vx * vx + vz * vz
            t = 0.0 if L2 < 1e-9 else max(0.0, min(1.0, ((x - ax) * vx + (z - az) * vz) / L2))
            best = min(best, math.hypot(x - ax - vx * t, z - az - vz * t))
    return best


def flag_piece(g, T, poly, top, f, tilt=None, bevel=.025, thin=False):
    """One flagstone (or a broken piece of one): a slab down into the earth (Low, or thin far off: its top only)."""
    if T.i == 2 or thin:
        t = mk.Geo()
        t.face([(p[0], top, p[1]) for p in poly], f, normal=(0, 1, 0))
        if tilt is not None:
            (px, py, pz), ax, ang = tilt
            t = t.transformed(mk.about((px, py, pz), sk.quat_axis(ax[0], ax[1], ax[2], ang)))
        g.extend(t)
        return
    mk.slab(g, poly, top - .1, top, f, T, bevel=bevel, tilt=tilt)


def flags(g, T, rec):
    """The flagstones: courses across x in running bond, each stone a little turned, some with a corner broken off, some
    cracked into pieces, some gone (earth and moss in their place); the ones near the pilot laid flat (within a centimetre),
    the ones over roots heaved and tipped; behind the gate the path's stones sparser and more broken."""
    r = mk.rng(12)
    lines = [[(p[0], p[2]) for p in pts] for pts, *_ in SURFACE_ROOTS]
    z = 6.3
    while z > STAIR_Z + .05:
        depth = float(r.uniform(.7, 1.05))
        z0 = max(STAIR_Z + .05, z - depth)
        if z > -3.45 and z0 < -3.45:
            z0 = -3.45
        x = -5.9 + float(r.uniform(0, .6))
        while x < 5.9:
            w = float(r.uniform(.7, 1.4))
            x1 = min(5.9, x + w)
            cx, cz = (x + x1) / 2, (z0 + z) / 2
            d0 = math.hypot(cx, cz)
            inside = paved(cx, cz)
            behind = cz < -3.45
            skip = inside < 0
            if behind and abs(cx) > PATH_HALF:
                skip = True
            if not skip and inside < 1.1 and r.uniform() < .45:
                skip = True
            if not skip and d0 > 2.4 and r.uniform() < (.3 if behind else .1):
                skip = True
            if abs(cx) < .8 and .45 < cz < 1.2:
                skip = True     # one stone gone just before the pilot: the earth the fangs strike
            for px_, pz_, rx_, rz_, _ in PUDDLES:
                if ((cx - px_) / (rx_ + .35)) ** 2 + ((cz - pz_) / (rz_ + .3)) ** 2 < 1:
                    skip = True
            if -3.75 < cz < -2.25 and abs(cx) > 1.3:
                skip = True     # the pillars' feet and the walls
            if -3.5 < cz < -2.5 and abs(cx) < 1.5:
                skip = True     # the threshold
            if skip or x1 - x < .25 or z - z0 < .25:
                x = x1
                continue
            near = d0 < 3.0
            thin = d0 > 5.2 or cz < -4.2
            gap = .012 if near else float(r.uniform(.015, .04))
            j1, j2 = float(r.uniform(-.03, .03)), float(r.uniform(-.03, .03))
            poly = [(x + gap, z0 + gap + j1), (x1 - gap, z0 + gap + j2), (x1 - gap, z - gap - j2 * .5), (x + gap, z - gap - j1 * .5)]
            yaw = float(r.uniform(-.015, .015)) if near else float(r.uniform(-.06, .06))
            ca, sa = math.cos(yaw), math.sin(yaw)
            poly = [(cx + (p[0] - cx) * ca - (p[1] - cz) * sa, cz + (p[0] - cx) * sa + (p[1] - cz) * ca) for p in poly]
            if r.uniform() < (.15 if near else .35):
                k = int(r.integers(0, 4))
                a, b, c = poly[k - 1], poly[k], poly[(k + 1) % 4]
                t1, t2 = float(r.uniform(.12, .35)), float(r.uniform(.12, .35))
                pa = (b[0] + (a[0] - b[0]) * t1, b[1] + (a[1] - b[1]) * t1)
                pc = (b[0] + (c[0] - b[0]) * t2, b[1] + (c[1] - b[1]) * t2)
                poly = poly[:k] + [pa, pc] + poly[k + 1:]
            pieces = [poly]
            if r.uniform() < (.2 if near else .3) and x1 - x > .4 and z - z0 > .4:
                pts = [(float(r.uniform(x + .12, x1 - .12)), float(r.uniform(z0 + .12, z - .12))) for _ in range(2)]
                pieces = [mk.shrink(c, .01) for c in mk.voronoi_cells(pts, poly) if len(c) >= 3]
            fl = F_FLAG_MOSS if r.uniform() < .22 else F_FLAG if r.uniform() < .62 else F_FLAG_DK
            for pc in pieces:
                pcx = sum(p[0] for p in pc) / len(pc)
                pcz = sum(p[1] for p in pc) / len(pc)
                dr = dist_to_lines(pcx, pcz, lines)
                dd = min(math.hypot(p[0], p[1]) for p in pc) + .3
                ax = (float(r.uniform(-1, 1)), 0.0, float(r.uniform(-1, 1)))
                if dd < 2.9:
                    top = float(r.uniform(-.008, .004))
                    ang = float(r.uniform(0, .012))
                elif dr < .55:
                    top = float(r.uniform(.02, .07))
                    ang = float(r.uniform(.06, .16))
                else:
                    top = float(r.uniform(-.035, .015))
                    ang = float(r.uniform(0, .05 if not behind else .09))
                tilt = ((pcx, top, pcz), ax, ang) if abs(ax[0]) + abs(ax[2]) > 1e-3 else None
                flag_piece(g, T, pc, top, fl, tilt=tilt, thin=thin and dr >= .55)
                rec.append((pc, top))
            x = x1
        z = z0


def puddles(g, T):
    r = mk.rng(21)
    for cx, cz, rx, rz, turn in PUDDLES:
        poly = blob2d(cx, cz, rx, rz, (12, 9, 7)[T.i], r, turn, .16)
        g.face([(p[0], -.024, p[1]) for p in poly], F_WATER, normal=(0, 1, 0))
        rim = blob2d(cx, cz, rx + .22, rz + .2, (12, 9, 7)[T.i], r, turn, .1)
        g.face([(p[0], -.038, p[1]) for p in rim], F_MUD, normal=(0, 1, 0))


def flag_top(x, z, rec):
    for pc, top in rec:
        if in_poly(x, z, pc):
            return top
    return None


def litter(g, T, rec):
    """Fallen leaves: single leaves lying curled on the flags and the earth, gathered in drifts against roots, walls and trunks,
    a few on the water."""
    r = mk.rng(31)
    fins = (F_LITTER, F_LITTER_OC, F_LITTER_DK, F_LITTER, F_LEAF_Y, F_LITTER_DK)
    drifts = ((-2.6, -2.15), (2.7, -2.25), (-4.0, -.5), (3.6, 1.6), (-3.4, 3.4), (4.6, -1.9), (-1.0, 5.4), (1.8, 6.0),
              (-5.0, 5.0), (5.3, 4.6), (0.4, -2.3), (.0, .85), (-1.2, -4.4), (1.1, -6.0), (-1.9, .6), (1.7, 1.2))
    count = (190, 110, 50)[T.i]
    placed = 0
    tries = 0
    while placed < count and tries < count * 8:
        tries += 1
        if r.uniform() < .55:
            cx, cz = drifts[int(r.integers(0, len(drifts)))]
            x, z = cx + float(r.normal(0, .45)), cz + float(r.normal(0, .35))
        else:
            a = float(r.uniform(0, 2 * math.pi))
            rad = float(r.uniform(0, 1) ** .7) * 9.0
            x, z = math.cos(a) * rad, math.sin(a) * rad * .9 + .3
        on_water = any(((x - px_) / rx_) ** 2 + ((z - pz_) / rz_) ** 2 < 1 for px_, pz_, rx_, rz_, _ in PUDDLES)
        if on_water and r.uniform() < .8:
            continue
        ft = flag_top(x, z, rec) if paved(x, z) > -.4 else None
        if on_water:
            y = -.02
        elif ft is not None:
            if ft > .03:
                continue
            y = ft + .004
        else:
            y = ground_y(x, z) + .006
        L = float(r.uniform(.12, .26))
        W = L * float(r.uniform(.35, .55))
        yaw = float(r.uniform(0, 2 * math.pi))
        d = (math.cos(yaw), math.sin(yaw))
        s_ = (-d[1], d[0])
        curl = float(r.uniform(.004, .016))
        tip = (x + d[0] * L * .5, y + curl * .5, z + d[1] * L * .5)
        bas = (x - d[0] * L * .5, y, z - d[1] * L * .5)
        a1 = (x + s_[0] * W * .5, y + curl, z + s_[1] * W * .5)
        a2 = (x - s_[0] * W * .5, y + curl, z - s_[1] * W * .5)
        g.face((bas, a1, tip, a2), fins[placed % len(fins)], normal=(0, 1, 0))
        placed += 1


# =============================================================================================================================
# The gateway: pillars, capitals, the carved lintel, the threshold
# =============================================================================================================================
def gateway(g, T):
    r = mk.rng(41)
    for side in (-1, 1):
        x = side * PX
        sd = 50 + (side > 0)
        # the plinth: one broad stone under each pillar, its edges worn
        block(g, T, (x, .1, GZ), (.64, .24, .62), F_STONE_DK, seed=sd, b=.07, rough=.018, chip=.12, cell=.6, skip=('-y',),
              q=qe(yaw=float(r.uniform(-.03, .03))))
        y = .34
        for k, hgt in enumerate((.6, .7, .48)):
            jx, jz = float(r.uniform(-.025, .025)), float(r.uniform(-.025, .025))
            if side < 0 and k == 1:
                jx, jz = .05, .035     # the fig has pushed this drum out of true
            hw = PH + float(r.uniform(-.02, .02))
            block(g, T, (x + jx, y + hgt / 2, GZ + jz), (hw, hgt / 2 - .01, hw), F_STONE, seed=sd * 10 + k, b=.06, rough=.018,
                  chip=.14 if k == 2 else .1, cell=.5, q=qe(yaw=float(r.uniform(-.04, .04))))
            y += hgt
        # the capital: a cushion under a wider slab
        block(g, T, (x, CAP_Y + .12, GZ), (.56, .115, .55), F_RELIEF, seed=sd * 7, b=.05, rough=.014, chip=.12, cell=.5)
        if T.detail:
            cartouche(g, T, (x, 1.29, GZ + PH + .004), 2 if side < 0 else 0, 1)
            moss_pad(g, T, (x + side * .25, L_LO + .001, GZ + .4), .2, F_MOSS, 70 + side, rz=.12)
    lintel(g, T)
    # the threshold: a long worn sill between the pillars, cracked through
    block(g, T, (-.35, -.03, GZ), (1.05, .07, .4), F_STONE, seed=61, b=.035, rough=.006, chip=.05, cell=.5, skip=('-y',))
    block(g, T, (1.06, -.035, GZ + .01), (.37, .068, .4), F_STONE, seed=62, b=.035, rough=.006, chip=.06, cell=.5,
          skip=('-y',), q=qe(yaw=.03, roll=.02))


def lintel(g, T):
    cx = (L_X0 + L_X1) / 2
    hx = (L_X1 - L_X0) / 2
    hy = (TOP - L_LO) / 2
    block(g, T, (cx, L_LO + hy, GZ), (hx, hy, L_HZ), F_STONE, seed=71, b=.07, rough=.016, chip=.11, cell=.32, flat_top=True,
          brk={'+x': .32})
    zf = GZ + L_HZ          # the front face
    zb = GZ - L_HZ          # the back face
    if not T.detail:
        rbox(g, T, (0, 2.75, zf + .01), (.55, .2, .04), F_RELIEF, seed=72)
        return
    # the bands: a moulding along the top and the foot of both faces, broken where it has spalled
    for zz, sgn in ((zf, 1), (zb, -1)):
        for y, runs in ((3.02, ((-2.66, -1.35), (-1.2, .78), (.95, 2.25))), (2.47, ((-2.68, -.62), (-.5, 1.55), (1.75, 2.2)))):
            for a, b in runs:
                sk.sweep(g, sk.rect_section(.12, .075), [(a, y, zz), (b, y, zz)], F_RELIEF, up=(0, 0, sgn))
    mask(g, T, (0.0, 2.745, zf))
    for k, xc in enumerate((-1.95, -1.08, 1.08)):
        cartouche(g, T, (xc, 2.745, zf), k, 1)
    cartouche(g, T, (1.92, 2.745, zf), 3, 1, broken=True)
    for k, xc in enumerate((-1.5, 0.0, 1.5)):
        cartouche(g, T, (xc, 2.745, zb), k + 1, -1)
    # a crack down the front, over the top and down the back near the broken end
    rr = mk.rng(73)
    for zz, sgn in ((zf + .012, 1), (zb - .012, -1)):
        pts = []
        for i in range(6):
            t = i / 5
            pts.append((.86 + float(rr.uniform(-.05, .05)) + .1 * t, TOP - .02 - (TOP - L_LO - .04) * t, zz))
        sk.sweep(g, sk.rect_section(.022, .012), pts, F_CRACK, up=(0, 0, sgn), caps=False)
    top = [(.86 + float(rr.uniform(-.04, .04)), TOP + .004, GZ + L_HZ * (1 - 2 * i / 4)) for i in range(5)]
    sk.sweep(g, sk.rect_section(.02, .006), top, F_CRACK, up=(0, 1, 0), caps=False)
    # moss on the lintel's ends, beards of it hanging from the foot of the front
    moss_pad(g, T, (-2.6, TOP + .003, -3.15), .22, F_MOSS, 51, droop=.02, rz=.2)
    moss_pad(g, T, (2.3, TOP + .003, -2.85), .2, F_MOSS_LT, 52, droop=.02, rz=.14)
    r = mk.rng(53)
    for k in range((8, 4, 0)[T.i]):
        x = float(r.uniform(-2.6, 2.3))
        strand(g, T, (x, L_LO + .02, zf + .015 - .03 * float(r.uniform(0, 1))), float(r.uniform(.25, .7)), .045,
               F_MOSS_LT if k % 2 else F_MOSS, 60 + k)


def cartouche(g, T, c, kind, sgn, broken=False):
    """A worn glyph panel: a raised frame and a glyph inside (a sun, a stepped fret, a spiral or a cross)."""
    x, y, z = c
    o = sgn * .018
    hw, hh = .3, .2
    sec = sk.rect_section(.06, .045)
    if broken:
        frame = [(x + .05, y - hh, z + o), (x - hw, y - hh, z + o), (x - hw, y + hh, z + o), (x - .1, y + hh, z + o)]
        sk.sweep(g, sec, frame, F_RELIEF, up=(0, 0, sgn))
        return
    frame = [(x - hw, y - hh, z + o), (x + hw, y - hh, z + o), (x + hw, y + hh, z + o), (x - hw, y + hh, z + o)]
    sk.sweep(g, sec, frame, F_RELIEF, closed=True, up=(0, 0, sgn))
    k = kind % 4
    lo_z, hi_z = min(z, z + o * 2.2), max(z, z + o * 2.2)
    if k == 0:
        ring = [(x + math.cos(a) * .11, y + math.sin(a) * .11, z + o) for a in np.linspace(0, 2 * math.pi, (10, 7, 6)[T.i], endpoint=False)]
        sk.sweep(g, sk.rect_section(.04, .035), ring, F_RELIEF, closed=True, up=(0, 0, sgn))
        rbox(g, T, (x, y, z + o * .8), (.04, .04, .025), F_RELIEF, seed=80 + kind, b=.012, jit=.006)
    elif k == 1:
        for i in range(3):
            yy = y - .1 + i * .1
            sk.box(g, x - .2 + i * .05, x + .2 - i * .05, yy - .028, yy + .028, lo_z, hi_z, F_RELIEF, T, bevel=.008, cell=0)
    elif k == 2:
        spiral(g, T, (x, y, z + o), .14, 1.8, (0, 0, sgn), F_RELIEF, sgn, sec=.035)
    else:
        sk.box(g, x - .035, x + .035, y - .15, y + .15, lo_z, hi_z, F_RELIEF, T, bevel=.008, cell=0)
        sk.box(g, x - .2, x + .2, y - .035, y + .035, lo_z, hi_z, F_RELIEF, T, bevel=.008, cell=0)


def mask(g, T, c):
    """The lintel's centre: a worn predator's mask (brow, ringed eyes, snout, a fanged mouth, scrolls at its cheeks)."""
    x, y, z = c
    block(g, T, (x, y, z + .012), (.62, .215, .04), F_RELIEF, seed=91, b=.025, rough=.006, chip=.04, cell=.3)
    zo = z + .055
    for side in (-1, 1):
        ex = x + side * .22
        ring = [(ex + math.cos(a) * .1, y + .06 + math.sin(a) * .085, zo) for a in np.linspace(0, 2 * math.pi, (10, 7, 6)[T.i], endpoint=False)]
        sk.sweep(g, sk.circle_section(.027, 4, math.pi / 4), ring, F_RELIEF, closed=True)
        mk.rock(g, (ex, y + .06, zo - .01), (.052, .046, .036), F_STONE_DK, T, seed=92 + side, rough=.06, flat_bottom=False,
                level=1, facets=False)
        spiral(g, T, (x + side * .48, y - .02, zo - .012), .1, 1.6, (0, 0, 1), F_RELIEF, side)
    brow = [(x - .42, y + .1, zo), (x - .22, y + .2, zo + .012), (x, y + .13, zo + .02), (x + .22, y + .2, zo + .012), (x + .42, y + .1, zo)]
    sk.sweep(g, sk.rect_section(.065, .055), sk.catmull(brow, (3, 2, 2)[T.i]), F_RELIEF, up=(0, 0, 1))
    rbox(g, T, (x, y - .025, zo + .005), (.085, .085, .045), F_RELIEF, seed=94, b=.025, jit=.01)
    rbox(g, T, (x, y - .13, zo - .005), (.37, .032, .035), F_RELIEF, seed=95, b=.012, jit=.006)
    for fx, L, rr in ((-.17, .23, .038), (.17, .23, .038), (-.31, .13, .026), (.31, .13, .026), (-.06, .08, .02), (.06, .08, .02)):
        sk.lathe(g, [(rr, 0), (rr * .7, L * .5), (0.0, L)], (x + fx, y - .15, zo), F_RELIEF, (5, 4, 4)[T.i], axis=(0, -1, 0.12))


# =============================================================================================================================
# The ruin round the gate: broken walls, fallen blocks, the stair and its shrine, the serpent head, the altar
# =============================================================================================================================
def wall_h(x):
    """The broken walls' remaining height at x: taller near the gate on the left, low and ragged under the fig on the right."""
    d = abs(x) - 2.6
    if x > 0:
        return max(.5, 2.5 - .26 * d + .45 * math.sin(x * 1.7) + .25 * math.sin(x * 4.1))
    return max(.5, 1.6 - .17 * d + .4 * math.sin(x * 2.3) + .25 * math.sin(x * 3.7))


def walls(g, T):
    """Broken walls running off either side of the gate: courses of big rough blocks in running bond, their tops ragged (stones
    shoved askew, corners knocked off), moss on them, blocks fallen at their feet."""
    r = mk.rng(51)
    crs = .5 if T.i < 2 else .75
    for side in (-1, 1):
        for c in range(6):
            y = c * crs - .14
            x = WALL_X0 + (.5 if c % 2 else 0.0) * float(r.uniform(.5, 1.0))
            k = 0
            while x < WALL_X1:
                L = float(r.uniform(.75, 1.3)) if T.i < 2 else float(r.uniform(1.4, 2.2))
                x1 = min(WALL_X1, x + L)
                mid = side * (x + x1) / 2
                hcut = wall_h(mid) + float(r.uniform(-.2, .2))
                if y + crs <= hcut + (.2 if T.i == 2 else 0) and x1 - x > .3:
                    top_course = y + 2 * crs > hcut
                    xa, xb = sorted((side * (x + .012), side * (x1 - .012)))
                    jz = float(r.uniform(-.05, .05))
                    yaw = float(r.uniform(-.05, .05))
                    roll = float(r.uniform(-.05, .05)) if top_course else float(r.uniform(-.012, .012))
                    if top_course and r.uniform() < .4:
                        jz += float(r.uniform(-.14, .14))
                        yaw += float(r.uniform(-.22, .22))
                    hy = crs / 2 - .012
                    rbox(g, T, ((xa + xb) / 2, y + hy, GZ - .05 + jz), ((xb - xa) / 2, hy, .42), F_STONE if (k + c) % 3 else F_STONE_DK,
                         seed=500 + side * 100 + c * 17 + k, b=.05, jit=.035,
                         chop=float(r.uniform(.08, .16)) if (top_course and r.uniform() < .6) else 0.0, q=qe(yaw=yaw, roll=roll))
                    if top_course and T.detail and r.uniform() < .45:
                        moss_pad(g, T, ((xa + xb) / 2 + float(r.uniform(-.2, .2)), y + crs - .01, GZ - .05 + jz),
                                 float(r.uniform(.2, .35)), F_MOSS if r.uniform() < .6 else F_MOSS_LT, 5000 + k * 7 + c, droop=.06,
                                 rz=.3)
                x = x1
                k += 1
    # fallen blocks at the walls' feet and in the court
    for k, (x, z, yaw, roll, sz) in enumerate(((-5.2, -1.9, .5, .35, (.42, .25, .38)), (3.0, -1.2, -.3, -.25, (.36, .22, .34)),
                                               (5.5, -1.5, .9, .5, (.46, .24, .4)), (-5.8, -.6, -.6, .2, (.4, .23, .38)),
                                               (6.7, -4.0, .2, .1, (.5, .25, .42)), (-6.5, -4.2, .4, .3, (.45, .24, .38)),
                                               (2.2, -4.6, .7, .2, (.4, .22, .36)))):
        if T.i == 2 and k > 3:
            break
        rbox(g, T, (x, sz[1] * .7 + ground_y(x, z) - .08, z), sz, F_STONE_DK, seed=600 + k, b=.05, jit=.04, chop=.12,
             q=qe(yaw=yaw, roll=roll))
        if T.detail and k % 2 == 0:
            moss_pad(g, T, (x, sz[1] * 1.6 + ground_y(x, z) - .1, z), sz[0] * .8, F_MOSS, 6000 + k, rz=sz[2] * .7, droop=.08)
    # the broken end of the lintel, fallen in the court, half sunk in the leaf mould
    block(g, T, (3.6, .18, -2.05), (.38, .41, .4), F_STONE, seed=73, b=.06, rough=.02, chip=.12, cell=.45,
          q=qe(yaw=.45, roll=1.2, pitch=.12), brk={'-y': .16})
    if T.detail:
        moss_pad(g, T, (3.55, .56, -2.1), .3, F_MOSS, 7)


def stair(g, T):
    """Behind the gate: a stair of long rough slabs (two pieces gone) between serpent balustrades up to a platform, and on it a
    ruined shrine: a doorway with its lintel, a corbelled roof half fallen, stubs of wall, the dark hollow inside."""
    r = mk.rng(61)
    zb = SHRINE_Z - .7
    for s_ in range(STAIR_N):
        zf = STAIR_Z - s_ * STAIR_RUN
        top = (s_ + 1) * STAIR_RISE
        x = -STAIR_HALF
        k = 0
        while x < STAIR_HALF - .1:
            L = 2 * STAIR_HALF if T.i == 2 else float(r.uniform(1.3, 2.4))
            x1 = min(STAIR_HALF, x + L)
            if STAIR_HALF - x1 < .5:
                x1 = STAIR_HALF
            gone = T.i < 2 and ((s_ == 1 and x < -1.2 and x1 > -1.6) or (s_ == 3 and x < 1.2 and x1 > .9))
            if gone:
                # a piece broken out of the step's front: the stone behind it shows, a shard lies below
                rbox(g, T, ((x + x1) / 2, (top - .3 - STAIR_RISE) / 2, (zf - .4 + zb) / 2), ((x1 - x) / 2 - .015, (top - STAIR_RISE + .3) / 2,
                     (zf - .4 - zb) / 2), F_STONE_DK, seed=720 + s_, b=.05, jit=.04, chop=.12)
                rbox(g, T, ((x + x1) / 2 + .2, top - STAIR_RISE + .12, zf + .3), (.35, .12, .22), F_STONE, seed=721 + s_, b=.04,
                     jit=.04, chop=.1, q=qe(yaw=.5, roll=.3))
            else:
                y0 = -.3
                rbox(g, T, ((x + x1) / 2, (y0 + top) / 2, (zf + zb) / 2 + float(r.uniform(-.03, .03))),
                     ((x1 - x) / 2 - .015, (top - y0) / 2, (zf - zb) / 2), F_STONE if (s_ + k) % 2 else F_STONE_DK,
                     seed=700 + s_ * 20 + k, b=.06, jit=.035, chop=.12 if r.uniform() < .5 else 0.0,
                     q=qe(yaw=float(r.uniform(-.015, .015)), roll=float(r.uniform(-.02, .02))))
            if T.detail and r.uniform() < .2:
                moss_pad(g, T, ((x + x1) / 2 + float(r.uniform(-.3, .3)), top + .002, zf - .25), .25, F_MOSS, 7000 + s_ * 9 + k,
                         rz=.18, droop=.02)
            x = x1
            k += 1
    # the balustrades: sloping serpent bodies on stepped side walls, the right one's head gone (it lies in the court)
    z0, y0 = STAIR_Z + .25, .5
    z1, y1 = PLAT_Z - .2, PLAT_Y + .4
    L = math.hypot(z0 - z1, y1 - y0)
    ang = math.atan2(y1 - y0, z0 - z1)
    for side in (-1, 1):
        x = side * (STAIR_HALF + .3)
        block(g, T, (x, (y0 + y1) / 2, (z0 + z1) / 2), (.27, .26, L / 2), F_STONE, seed=740 + side, b=.07, rough=.02, chip=.1,
              cell=.8, q=qe(pitch=ang), brk={'+z': .25} if side < 0 else None)
        steps = range(0, STAIR_N + 1, 2) if T.i < 2 else (2,)
        for s_ in steps:
            zz = STAIR_Z - s_ * STAIR_RUN + .1
            hh = y0 + (y1 - y0) * min(1.0, max(0.0, (STAIR_Z + .25 - zz) / (z0 - z1))) - .2
            if T.i == 2:
                rbox(g, T, (x, (y0 + y1) / 2 - .5, (z0 + z1) / 2), (.3, (y0 + y1) / 2 - .2, L / 2), F_STONE_DK, seed=750 + side,
                     b=.04, jit=.03)
                continue
            rbox(g, T, (x, (hh - .3) / 2, zz - STAIR_RUN), (.3, (hh + .3) / 2, STAIR_RUN - .01), F_STONE_DK,
                 seed=750 + s_ * 3 + side, b=.04, jit=.03)
        if side > 0:
            # the surviving head at the foot: a blunt snout, ringed eyes, two fangs
            hz = z0 + .2
            block(g, T, (x, y0 + .12, hz), (.3, .26, .3), F_STONE, seed=745, b=.08, rough=.02, chip=.08, cell=.6)
            for s2 in (-1, 1):
                mk.rock(g, (x + s2 * .3, y0 + .22, hz + .1), (.06, .07, .07), F_RELIEF, T, seed=746, rough=.1, flat_bottom=False,
                        level=0)
                sk.lathe(g, [(.04, 0), (.025, .08), (0.0, .16)], (x + s2 * .14, y0 - .08, hz + .26), F_RELIEF, (5, 4, 3)[T.i],
                         axis=(0, -1, .1))
    # the shrine
    base = PLAT_Y
    sz = SHRINE_Z
    for side in (-1, 1):
        if T.i == 2:
            rbox(g, T, (side * 1.05, base + .93, sz), (.32, .93, .45), F_STONE, seed=760 + side, b=.04, jit=.03)
            rbox(g, T, (side * 2.5, base + .6, sz - .05), (1.1, .6, .42), F_STONE_DK, seed=770 + side, b=.04, jit=.04, chop=.15)
            continue
        for c in range(3):
            rbox(g, T, (side * 1.05 + float(r.uniform(-.03, .03)), base + c * .62 + .3, sz), (.32, .3, .45), F_STONE,
                 seed=760 + c * 3 + side, b=.04, jit=.03, chop=.08 if c == 2 else 0.0, q=qe(yaw=float(r.uniform(-.04, .04))))
        for c in range(3 if side < 0 else 2):
            for xs_ in (1.75, 2.6):
                rbox(g, T, (side * xs_, base + c * .55 + .27, sz - .05), (.4, .27, .42), F_STONE_DK,
                     seed=780 + c * 7 + int(xs_ * 10) + side, b=.04, jit=.04, chop=.1 if c == (2 if side < 0 else 1) else 0.0)
    # the door's lintel, cracked and sagging, and the corbelled roof stepping in over it (its left half fallen)
    rbox(g, T, (-.05, base + 2.1, sz), (1.45, .22, .5), F_STONE, seed=790, b=.05, jit=.03, q=qe(roll=.03))
    for c, (w, yy) in enumerate(((1.25, 2.55), (.95, 2.97), (.62, 3.36))):
        if T.i == 2 and c:
            break
        rbox(g, T, (-.35 - .1 * c, base + yy, sz - .02), (w * .72, .2, .46), F_STONE_DK, seed=791 + c, b=.04, jit=.04,
             chop=.12, q=qe(roll=.04 * c))
    rbox(g, T, (1.6, base + .2, sz + 1.0), (.5, .2, .42), F_STONE, seed=795, b=.04, jit=.04, chop=.14, q=qe(yaw=.6, roll=.25))
    sk.box(g, -.75, .75, base - .05, base + 1.9, sz - 1.4, sz - .44, F_HOLLOW, T, bevel=0, cell=0, skip=('+z',))
    if T.detail:
        moss_pad(g, T, (-.4, base + 3.58, sz), .35, F_MOSS, 797, rz=.3)
        moss_pad(g, T, (2.5, base + 1.1, sz - .05), .3, F_MOSS_LT, 798, rz=.3)


def serpent(g, T):
    """A fallen serpent head (the stair balustrade's): a square-jawed head with ringed eyes under heavy brows, a fanged open
    mouth, scales over its crown and a collar of feathers, toppled on its side and half sunk at the court's right."""
    h = mk.Geo()
    block(h, T, (0, .52, -.05), (.42, .3, .56), F_STONE, seed=801, b=.1, rough=.02, chip=.09, cell=.36)
    block(h, T, (0, .53, .62), (.36, .17, .2), F_STONE, seed=802, b=.08, rough=.016, chip=.07, cell=.4)
    block(h, T, (0, .14, .22), (.38, .14, .6), F_STONE_DK, seed=803, b=.08, rough=.018, chip=.07, cell=.42)
    sk.box(h, -.28, .28, .27, .37, .25, .72, F_HOLLOW, T, bevel=0, cell=0)
    for side in (-1, 1):
        sk.lathe(h, [(.05, 0), (.033, .11), (0.0, .22)], (side * .22, .37, .74), F_RELIEF, (5, 4, 3)[T.i], axis=(0, -1, -.15))
        sk.lathe(h, [(.032, 0), (.02, .06), (0.0, .11)], (side * .1, .37, .78), F_RELIEF, (4, 4, 3)[T.i], axis=(0, -1, 0))
        ex = side * .42
        ring = [(ex, .68 + math.sin(a) * .1, .24 + math.cos(a) * .12) for a in np.linspace(0, 2 * math.pi, (9, 7, 5)[T.i], endpoint=False)]
        sk.sweep(h, sk.circle_section(.034, 4, math.pi / 4), ring, F_RELIEF, closed=True)
        mk.rock(h, (ex - side * .03, .68, .24), (.065, .075, .085), F_STONE_DK, T, seed=804 + side, rough=.06, flat_bottom=False,
                level=1, facets=False)
        brow = [(side * .2, .87, .5), (side * .36, .87, .32), (side * .45, .82, .1)]
        sk.sweep(h, sk.circle_section(.058, (5, 4, 4)[T.i]), sk.catmull(brow, (3, 2, 2)[T.i]), F_RELIEF)
        if T.detail:
            spiral(h, T, (side * .16, .63, .83), .07, 1.5, (0, 0, 1), F_RELIEF, side)
    if T.detail:
        for zz in (-.15, -.33, -.51):
            arc = [(math.cos(a) * .45, .52 + math.sin(a) * .34, zz) for a in np.linspace(.15, math.pi - .15, (6, 4, 3)[T.i])]
            sk.sweep(h, sk.circle_section(.034, 4), arc, F_RELIEF)
    nf = (6, 5, 3)[T.i]
    for k in range(nf):
        a = math.pi * (.1 + .8 * k / (nf - 1))
        d = (math.cos(a), math.sin(a))
        rbox(h, T, (d[0] * .48, .3 + d[1] * .48, -.66), (.11, .3, .06), F_RELIEF, seed=810 + k, b=.04, jit=.02, chop=.06 * (k % 2),
             q=qe(roll=a - math.pi / 2, pitch=-.25))
    limb(h, T, [(0, .3, .7), (.03, .2, .88), (.0, .08, 1.0), (-.04, .05, 1.06)], .05, .03, F_STONE_DK, sides=4, flat=.45)
    m = sk.affine(qe(yaw=.72, roll=.45, pitch=.06), (-3.65, -.28, -1.2), (1.4, 1.4, 1.4))
    g.extend(h.transformed(m))
    if T.detail:
        moss_pad(g, T, (-3.65, 1.0, -1.45), .42, F_MOSS, 9, rz=.3)
        moss_pad(g, T, (-4.05, .5, -1.95), .3, F_MOSS_LT, 10)


def altar(g, T):
    """A stepped altar at the court's left: three courses of stone (a corner stone fallen), a broken stele on top with a worn
    spiral and bars carved on its face."""
    r = mk.rng(71)
    cx, cz = 4.4, -.75
    q = qe(yaw=.18)
    R = rotm(q)
    y = 0.0
    for c, (half, hh) in enumerate(((1.15, .17), (.86, .16), (.6, .15))):
        for k, (sx, sz) in enumerate(((-1, -1), (1, -1), (1, 1), (-1, 1))):
            if c == 0 and k == 2:
                continue        # a corner stone gone
            off = R @ np.array([sx * half / 2, 0, sz * half / 2])
            jit = (float(r.uniform(-.025, .025)), float(r.uniform(-.025, .025)))
            extra = .07 if c == 0 else 0.0
            rbox(g, T, (cx + off[0] + jit[0], y + hh - extra, cz + off[2] + jit[1]), (half / 2 - .012, hh + extra, half / 2 - .012),
                 F_STONE if (c + k) % 2 else F_STONE_DK, seed=900 + c * 10 + k, b=.04, jit=.025,
                 chop=.1 if r.uniform() < .5 else 0.0, q=qmul(q, qe(yaw=float(r.uniform(-.04, .04)))))
        if c == 0:
            off = R @ np.array([half * .78, 0, half * .8])
            rbox(g, T, (cx + off[0], .16, cz + off[2]), (half / 2 - .03, .17, half / 2 - .05), F_STONE_DK, seed=909, b=.04,
                 jit=.03, chop=.14, q=qmul(q, qe(roll=.5, yaw=.3)))
        y += 2 * hh
    # the stele: its stump standing on the altar (bars carved on it), its top fallen face up in the court (a worn spiral)
    sh = .3
    qs = qmul(q, qe(roll=.04))
    block(g, T, (cx, y + sh, cz), (.34, sh, .12), F_RELIEF, seed=921, b=.03, rough=.012, chip=.07, cell=.25, q=qs,
          brk={'+y': .14})
    qt = qe(yaw=.6, pitch=-1.42)
    ct = (3.4, .08, .35)
    block(g, T, ct, (.34, .3, .12), F_RELIEF, seed=922, b=.03, rough=.012, chip=.08, cell=.25, q=qt, brk={'-y': .12})
    if T.detail:
        st = mk.Geo()
        for yy in (-.05, -.17):
            sk.box(st, -.22, .22, yy - .03, yy + .03, .11, .15, F_RELIEF, T, bevel=.008, cell=0)
        g.extend(st.transformed(sk.affine(qs, (cx, y + sh, cz))))
        tp = mk.Geo()
        spiral(tp, T, (0, .05, .13), .17, 2.0, (0, 0, 1), F_RELIEF, 1, sec=.035)
        g.extend(tp.transformed(sk.affine(qt, ct)))
        moss_pad(g, T, (cx + .55, .99, cz - .45), .25, F_MOSS_LT, 11)
        moss_pad(g, T, (cx - .7, .68, cz + .2), .3, F_MOSS, 12)


# =============================================================================================================================
# Trees: buttressed trunks, the strangler fig's roots over the gate, roots over the flags, a fallen log
# =============================================================================================================================
def trunk(g, T, base, height, r0, f, seed, fins=5, reach=1.5, fin_h=1.6, lean=(0.0, 0.0), flute=.05, fluten=9, taper=.68,
          sink=.5):
    """A rainforest trunk: buttress fins (thin planks flaring down to the ground, `fins` of them reaching `reach` m out) round
    a fluted core rising `height` m, leaning by `lean` at its top. The base (with the fins) is a ring grid dense round each
    fin, stitched to a plainer tube above."""
    rr = mk.rng(seed)
    x0, y0, z0 = base
    a0 = float(rr.uniform(0, 2 * math.pi))
    fa = sorted(a0 + 2 * math.pi * i / fins + float(rr.uniform(-.22, .22)) * 2 * math.pi / fins for i in range(fins))
    fL = [reach * float(rr.uniform(.6, 1.15)) for _ in range(fins)]
    fH = [fin_h * float(rr.uniform(.75, 1.35)) for _ in range(fins)]
    hw = [.12 / (r0 + .5 * L) for L in fL]
    mults = ((-2.6, -1.1, 0.0, 1.1, 2.6), (-2.2, 0.0, 2.2), (-1.3, 0.0, 1.3))[T.i]
    step = 2 * math.pi / (18, 12, 8)[T.i]
    ang = []
    for i in range(fins):
        for m in mults:
            ang.append(fa[i] + m * hw[i])
        a1 = fa[i] + mults[-1] * hw[i]
        b0 = (fa[(i + 1) % fins] + (2 * math.pi if i == fins - 1 else 0)) + mults[0] * hw[(i + 1) % fins]
        nfill = max(0, int(round((b0 - a1) / step)) - 1)
        for k in range(nfill):
            ang.append(a1 + (b0 - a1) * (k + 1) / (nfill + 1))
    phase = float(rr.uniform(0, 6))

    def centre(h):
        s = max(0.0, min(1.0, h / height))
        return (x0 + lean[0] * s * s, y0 + h, z0 + lean[1] * s * s)

    def core(h):
        s = max(0.0, min(1.0, h / height))
        return r0 * (1 + .3 * math.exp(-max(h, 0) / .55)) * (1 - (1 - taper) * s)
    base_h = ([-sink, 0.0, .25, .6, 1.1, 1.9, 3.3], [-sink, 0.0, .35, .9, 1.7, 3.3], [-sink, 0.0, .7, 1.8, 3.3])[T.i]
    up_h = ([3.8, 5.6, 7.6, height], [4.0, 7.0, height], [4.2, height])[T.i]
    V = []
    faces = []
    rings = []
    for h in base_h:
        c = centre(h)
        ring = []
        hh = max(h, 0.0)
        for a in ang:
            rad = core(h) * (1 + flute * math.sin(fluten * a + 1.7 * h + phase) + .05 * math.sin(3 * a + h * .9 + phase * 2))
            for i in range(fins):
                d = (a - fa[i] + math.pi) % (2 * math.pi) - math.pi
                L = fL[i] * 1.12 if h < 0 else fL[i] * max(0.0, 1 - hh / fH[i]) ** 1.7
                rad += L / (1 + (d / hw[i]) ** 4)
            ring.append(len(V))
            V.append((c[0] + math.cos(a) * rad, c[1], c[2] + math.sin(a) * rad))
        rings.append(ring)
    n = len(ang)
    for j in range(len(rings) - 1):
        for i in range(n):
            i1 = (i + 1) % n
            faces.append((rings[j][i], rings[j][i1], rings[j + 1][i1], rings[j + 1][i]))
    m = (12, 9, 7)[T.i]
    ua = [ang[0] + 2 * math.pi * k / m for k in range(m)]
    urings = []
    for h in up_h:
        c = centre(h)
        ring = []
        for a in ua:
            rad = core(h) * (1 + flute * .7 * math.sin(fluten * a + 1.7 * h + phase))
            ring.append(len(V))
            V.append((c[0] + math.cos(a) * rad, c[1], c[2] + math.sin(a) * rad))
        urings.append(ring)
    top = rings[-1]
    i = j = 0
    while i < n or j < m:
        ta1 = ang[(i + 1) % n] + (2 * math.pi if i + 1 >= n else 0)
        tb1 = ua[(j + 1) % m] + (2 * math.pi if j + 1 >= m else 0)
        if j >= m or (i < n and ta1 <= tb1):
            faces.append((top[i % n], top[(i + 1) % n], urings[0][j % m]))
            i += 1
        else:
            faces.append((top[i % n], urings[0][(j + 1) % m], urings[0][j % m]))
            j += 1
    for k in range(len(urings) - 1):
        for i2 in range(m):
            i3 = (i2 + 1) % m
            faces.append((urings[k][i2], urings[k][i3], urings[k + 1][i3], urings[k + 1][i2]))
    emit(g, V, faces, f, smooth=True, orient='volume')
    return centre, core


def trees(g, T):
    r = mk.rng(81)
    for k, (x, z, r0, H, fins, reach, seed, bark) in enumerate(TREES):
        y0 = ground_y(x, z) - .06
        lean = (float(r.uniform(-1.2, 1.2)), float(r.uniform(-1.2, 1.2)))
        fig = k == 0
        centre, core = trunk(g, T, (x, y0, z), H, r0, bark, 100 + seed, fins=fins, reach=reach, fin_h=1.7 if fig else 1.45,
                             lean=lean, flute=.13 if fig else .045, fluten=7 if fig else 10)
        # limbs up into the leaf masses
        for b in range((2, 2, 1)[T.i]):
            h0 = H * float(r.uniform(.6, .75))
            c0 = centre(h0)
            a = float(r.uniform(0, 2 * math.pi)) if b == 0 else a + math.pi * float(r.uniform(.6, 1.2))
            L = float(r.uniform(3.0, 5.0))
            pts = [c0, (c0[0] + math.cos(a) * L * .45, c0[1] + 1.4, c0[2] + math.sin(a) * L * .45),
                   (c0[0] + math.cos(a) * L, c0[1] + 2.4 + float(r.uniform(0, 1.2)), c0[2] + math.sin(a) * L)]
            limb(g, T, pts, core(h0) * .45, core(h0) * .18, bark, sides=(6, 5, 4)[T.i], per=(2, 2, 1)[T.i])
        # bracket fungi glowing faintly on the trunks near the court
        if T.detail and k in (0, 2, 4):
            fr = mk.rng(200 + k)
            for i in range(2):
                h = float(fr.uniform(.7, 2.2))
                c = centre(h)
                a = float(fr.uniform(0, 2 * math.pi))
                rad = core(h) * .97
                p = (c[0] + math.cos(a) * rad, c[1], c[2] + math.sin(a) * rad)
                for i2 in range(2):
                    rr_ = float(fr.uniform(.07, .11)) * (1 - .2 * i2)
                    q_ = (p[0] + math.cos(a) * .02 * i2, p[1] - i2 * .1, p[2] + math.sin(a) * .02 * i2)
                    sk.lathe(g, [(rr_, 0), (rr_ * .9, .016), (0.0, .028)], q_, F_FUNGUS, (6, 5, 4)[T.i], scale=(1.0, .6))
    log(g, T)


def log(g, T):
    """A fallen trunk on the camera's side: mossy, its ends broken, a bracket of fungus."""
    p0, p1 = (-5.6, .32, 10.2), (-.6, .42, 11.6)
    sides = (10, 8, 6)[T.i]
    path = [p0, sk._lerp(p0, p1, .5), p1]
    sk.sweep(g, sk.circle_section(1.0, sides), path, F_LOG, caps=False, scale=[.5, .47, .44])
    for p, rad, sgn in ((p0, .5, -1), (p1, .44, 1)):
        d = sk._norm(sk._sub(p1, p0))
        mk.rock(g, sk._add(p, d, sgn * .02), (rad * .95, rad * .95, rad * .95), F_HEART, T, seed=301 if sgn > 0 else 302,
                rough=.35, flat_bottom=False, level=1, facets=False)
    if T.detail:
        for k in range(4):
            c = sk._lerp(p0, p1, .15 + .22 * k)
            moss_pad(g, T, (c[0], c[1] + .43, c[2] + .05), .34, F_MOSS if k % 2 else F_MOSS_LT, 40 + k, droop=.14)
        c = sk._lerp(p0, p1, .62)
        for i in range(3):
            sk.lathe(g, [(.1 - i * .015, 0), (.09 - i * .015, .02), (0.0, .035)], (c[0] + .12 * i, c[1] - .05 - i * .1, c[2] - .42),
                     F_FUNGUS, (6, 5, 4)[T.i], scale=(1.0, .6))


# the strangler fig's aerial roots over the gate's right end: (control points, radius at the top, at the foot, flat)
FIG_ROOTS = (
    (((-3.55, 5.6, -4.65), (-2.95, 4.3, -3.6), (-2.62, 3.27, -3.05), (-2.6, 3.0, -2.5), (-2.48, 2.1, -2.48),
      (-2.42, 1.0, -2.5), (-2.55, .12, -2.2), (-2.9, -.06, -1.6)), .16, .07, .8),
    (((-3.4, 4.9, -4.9), (-2.85, 3.6, -3.75), (-2.7, 3.26, -3.25), (-2.83, 2.7, -3.48), (-2.42, 1.6, -3.47),
      (-2.4, .5, -3.5), (-2.6, -.05, -3.8)), .13, .06, .75),
    (((-3.6, 3.6, -4.5), (-3.0, 2.6, -3.2), (-2.42, 2.0, -2.75), (-2.38, 1.2, -3.0), (-2.45, .3, -3.2),
      (-2.8, -.05, -3.0)), .1, .05, .7),
    (((-3.3, 5.0, -4.3), (-2.62, 4.0, -3.4), (-2.52, 3.28, -3.0), (-2.5, 3.08, -2.54), (-2.3, 2.62, -2.5),
      (-2.05, 2.42, -2.58)), .075, .035, .7),
    (((-4.2, 4.0, -4.2), (-3.4, 2.4, -3.3), (-3.0, 1.0, -3.0), (-3.1, .15, -2.6), (-3.5, -.05, -2.0)), .12, .06, .8),
    (((-3.7, 6.4, -4.4), (-3.1, 5.0, -3.4), (-3.2, 3.2, -2.6), (-3.5, 1.4, -2.3), (-3.9, -.05, -2.4)), .09, .05, .9),
)


def fig_roots(g, T):
    for k, (pts, r0, r1, flat) in enumerate(FIG_ROOTS):
        if T.i == 2 and k > 2:
            break
        limb(g, T, pts, r0, r1, F_ROOT_PALE, flat=flat, per=(3 if k < 3 else 2, 2, 2)[T.i], ease=.7)


def surface_roots(g, T):
    for k, (pts, r0, r1) in enumerate(SURFACE_ROOTS):
        if T.i == 2 and k % 2:
            continue
        limb(g, T, pts, r0, r1, F_ROOT, flat=.6, per=(2, 2, 1)[T.i], up=(0, 1, 0), ease=.8)
        if T.detail and len(pts) > 3:
            a, b = pts[1], pts[2]
            mid = sk._lerp(a, b, .5)
            dx, dz = b[2] - a[2], -(b[0] - a[0])
            L = math.hypot(dx, dz) or 1
            tip = (mid[0] + dx / L * .7, -.03, mid[2] + dz / L * .7)
            if math.hypot(tip[0], tip[2]) > 2.6:
                limb(g, T, [mid, sk._lerp(mid, tip, .5), tip], r0 * .5, .015, F_ROOT, sides=4, per=2, flat=.6, up=(0, 1, 0))


# =============================================================================================================================
# Vines, lianas, plants
# =============================================================================================================================
def vines(g, T):
    """Hanging vines from the canopy (kept clear of the pounce's line: nothing below 5.2 m over x +-1.8 from the lintel to the
    pilot), lianas looping between the trunks, a few vines with leaves."""
    r = mk.rng(91)
    count = (13, 11, 8)[T.i]
    k = 0
    tries = 0
    while k < count and tries < 400:
        tries += 1
        x = float(r.uniform(-8.5, 8.5))
        z = float(r.uniform(-8.0, 9.5))
        bottom = float(r.uniform(1.2, 6.0))
        if abs(x) < 1.8 and -3.8 < z < 1.2 and bottom < 5.2:
            continue
        if math.hypot(x, z) < 2.6 and bottom < 2.4:
            continue
        if abs(x - 6.0) < 1.2 and abs(z) < 1.2:
            continue        # the side camera's spot
        if abs(x) < .8 and 5.3 < z < 6.7 and bottom < 2.2:
            continue        # the front camera's spot
        top = float(r.uniform(8.5, 10.5))
        pts = []
        for i in range(5):
            t = i / 4
            pts.append((x + float(r.uniform(-.25, .25)) * t, top + (bottom - top) * t, z + float(r.uniform(-.25, .25)) * t))
        sk.tube(g, sk.catmull(pts, (2, 2, 1)[T.i]), float(r.uniform(.014, .032)), F_VINE, (4, 3, 3)[T.i], caps=False)
        if T.detail and k % 3 == 0:
            for i in range((4, 3, 0)[T.i]):
                t = .5 + .5 * i / 5
                p = (x + float(r.uniform(-.06, .06)), top + (bottom - top) * t, z + float(r.uniform(-.06, .06)))
                a = float(r.uniform(0, 2 * math.pi))
                leaf_card(g, p, (math.cos(a), -.35, math.sin(a)), float(r.uniform(.14, .22)), float(r.uniform(.08, .11)),
                          F_LEAF if i % 2 else F_LEAF_LT, F_LEAF_DK)
        k += 1
    lianas = (((-3.6, 7.5, -4.6), (0.0, 6.3, -5.5), (4.8, 7.2, -5.2)), ((-3.8, 6.0, -4.8), (-5.5, 3.5, -1.5), (-6.6, 6.5, 1.0)),
              ((5.0, 5.5, -5.2), (6.6, 2.4, -2.0), (8.2, 6.0, 2.6)), ((-4.2, 6.5, 7.9), (0.0, 4.6, 9.5), (4.4, 6.8, 8.7)),
              ((-6.7, 8.0, 1.4), (-5.6, 2.0, 4.6), (-4.6, 7.0, 7.8)), ((4.9, 4.4, -5.2), (3.9, 1.6, -4.0), (3.3, .05, -3.6)),
              ((-3.7, 6.8, -4.7), (-1.0, 7.4, -1.0), (2.2, 8.6, 2.5)))
    for k, (a, m, b) in enumerate(lianas):
        if T.i == 2 and k > 3:
            break
        pts = [a, sk._lerp(a, m, .5), m, sk._lerp(m, b, .5), b]
        pts[1] = (pts[1][0], pts[1][1] - .5, pts[1][2])
        pts[3] = (pts[3][0], pts[3][1] - .5, pts[3][2])
        limb(g, T, pts, .09, .055, F_LIANA, sides=(4, 4, 3)[T.i], per=(3, 2, 2)[T.i])


def frond(g, T, base, yaw, pitch, length, width, f, droop=.55, n=None, cols=3):
    """A fern frond: a serrated two-sided blade along an arching rachis (its teeth suggest the pinnae; three columns fold it
    along the rachis)."""
    n = n or (6, 4, 3)[T.i]
    d0 = (math.cos(pitch) * math.cos(yaw), math.sin(pitch), math.cos(pitch) * math.sin(yaw))
    side = sk._norm(sk._cross(d0, (0, 1, 0)))
    G = []
    for i in range(n + 1):
        u = i / n
        c = sk._add(base, d0, length * u)
        c = (c[0], c[1] - droop * length * u * u, c[2])
        w = width * math.sin(math.pi * min(1.0, .1 + u * .95)) ** .6 * (1 - .3 * u)
        if 0 < i < n and i % 2 == 0:
            w *= .55
        dip = w * .3
        le = (c[0] - side[0] * w, c[1] - dip, c[2] - side[2] * w)
        ri = (c[0] + side[0] * w, c[1] - dip, c[2] + side[2] * w)
        G.append([le, c, ri] if cols == 3 else [le, ri])
    sheet(g, G, f)


def fern(g, T, at, scale=1.0, seed=0, fronds=None, y=None):
    r = mk.rng(seed)
    x, z = at
    if y is None:
        y = ground_y(x, z) if paved(x, z) < -.2 else 0.0
    rich = T.i == 0 and scale >= 1.25 and math.hypot(x, z) < 6.5
    n = fronds or ((6 if rich else 5), 5, 4)[T.i]
    a0 = float(r.uniform(0, 2 * math.pi))
    for k in range(n):
        yaw = a0 + 2 * math.pi * k / n + float(r.uniform(-.25, .25))
        pitch = float(r.uniform(.6, 1.1))
        L = scale * float(r.uniform(.8, 1.15))
        frond(g, T, (x, y + .04, z), yaw, pitch, L, L * .16, F_FERN if k % 3 else F_FERN_DK, droop=float(r.uniform(.6, .95)),
              cols=3 if rich else 2, n=4 if T.i == 0 else None)


def elephant(g, T, at, scale=1.0, seed=0, n=None):
    """Elephant ears: big drooping leaves on long stalks from one clump."""
    r = mk.rng(seed)
    x, z = at
    y = ground_y(x, z)
    n = n or (3, 3, 2)[T.i]
    for k in range(n):
        yaw = float(r.uniform(0, 2 * math.pi))
        hp = scale * float(r.uniform(.7, 1.25))
        out = scale * float(r.uniform(.25, .55))
        tip = (x + math.cos(yaw) * out, y + hp, z + math.sin(yaw) * out)
        limb(g, T, [(x, y - .05, z), (x + math.cos(yaw) * out * .3, y + hp * .6, z + math.sin(yaw) * out * .3), tip],
             .025 * scale, .016 * scale, F_LEAF_DK, sides=(4, 3, 3)[T.i], per=(2, 2, 1)[T.i])
        L = scale * float(r.uniform(.85, 1.2))
        W = L * .36
        nn = (4, 3, 2)[T.i]
        d = (math.cos(yaw), math.sin(yaw))
        s_ = (-d[1], d[0])
        G = []
        for i in range(nn + 1):
            u = i / nn
            c = (tip[0] + d[0] * L * .8 * u, tip[1] + .05 - L * (.15 * u + .55 * u * u), tip[2] + d[1] * L * .8 * u)
            w = W * math.sin(math.pi * min(1.0, .18 + .82 * u)) ** .8 * (1.15 - .4 * u)
            drop = w * .35
            G.append([(c[0] - s_[0] * w, c[1] - drop, c[2] - s_[1] * w), c, (c[0] + s_[0] * w, c[1] - drop, c[2] + s_[1] * w)])
        sheet(g, G, F_LEAF if k % 2 else F_LEAF_LT, fb=F_LEAF_DK)


def blade(g, base, d, L, W, f, droop=.45, n=2):
    """A sedge blade: a narrow two-sided strip arching out and over."""
    d = sk._norm(d)
    side = sk._norm(sk._cross(d, (0, 1, 0))) if abs(d[1]) < .95 else (1.0, 0.0, 0.0)
    G = []
    for i in range(n + 1):
        u = i / n
        c = sk._add(base, d, L * u)
        c = (c[0], c[1] - droop * L * u * u, c[2])
        w = W * (1 - .85 * u)
        G.append([sk._add(c, side, -w), sk._add(c, side, w)])
    sheet(g, G, f)


def tuft(g, T, at, n=None, L=.5, seed=0, f=F_LEAF, y=None):
    r = mk.rng(seed)
    x, z = at
    if y is None:
        y = ground_y(x, z) if paved(x, z) < -.2 else 0.0
    for k in range(n or (7, 5, 3)[T.i]):
        a = float(r.uniform(0, 2 * math.pi))
        blade(g, (x + math.cos(a) * .03, y, z + math.sin(a) * .03), (math.cos(a) * .55, 1.0, math.sin(a) * .55),
              L * float(r.uniform(.7, 1.2)), .024, f if k % 3 else F_LEAF_Y, droop=.5, n=(2, 2, 1)[T.i])


def plants(g, T):
    """Fern fans, elephant ears, sedges and leafy clumps: at the gate's feet, along the walls, round the trees, beside the path
    and the stair, and thick on the camera's side so a reverse angle is framed."""
    ferns = ((-2.75, -2.0, 1.1), (3.0, -2.55, 1.05), (-5.0, -2.3, 1.45), (5.2, -2.6, 1.4), (-4.6, 1.4, 1.35), (5.9, .8, 1.3),
             (-3.4, 4.6, 1.5), (3.2, 5.6, 1.45), (-1.9, 7.3, 1.6), (1.7, 7.7, 1.5), (-6.0, 6.0, 1.6), (6.1, 6.4, 1.6),
             (-2.3, -5.0, 1.25), (2.4, -6.0, 1.25), (0.4, 9.3, 1.6), (-2.8, -7.6, 1.2), (2.9, -8.0, 1.2))
    for k, (x, z, s) in enumerate(ferns):
        if T.i == 2 and k % 2:
            continue
        if T.i == 1 and k > 13:
            continue
        fern(g, T, (x, z), s, 1000 + k)
    ears = ((-5.6, 3.6, 1.2), (5.0, 3.2, 1.1), (-2.6, 8.4, 1.35), (2.6, 9.4, 1.3), (-7.4, -1.0, 1.25), (7.3, -.6, 1.2),
            (-2.6, -6.6, 1.05))
    for k, (x, z, s) in enumerate(ears):
        if T.i == 2 and k > 2 or k > 5:
            break
        elephant(g, T, (x, z), s, 1100 + k)
    tufts = ((-2.35, -1.7), (2.45, -1.85), (-1.25, -3.6), (1.05, -3.65), (3.9, .9), (-4.0, -.1), (-2.2, 3.8), (2.3, 3.4),
             (-.6, 6.6), (4.4, -3.4), (-.9, -5.6), (-2.9, 1.15))
    for k, (x, z) in enumerate(tufts):
        if T.i == 2 and k > 4:
            break
        if T.i == 0 and k > 9:
            break
        tuft(g, T, (x, z), n=(4, 4, 3)[T.i], seed=1200 + k, L=float(.35 + .1 * (k % 3)), f=F_LEAF if k % 2 else F_LEAF_LT)
    if T.i < 2:
        patches = ((-6.6, -.8), (6.7, -.6), (-6.9, 2.8), (6.6, 3.0), (-4.9, 6.9), (4.3, 7.3), (-3.6, -5.6), (3.7, -5.4),
                   (-2.2, 9.7), (2.1, 10.5), (-5.2, -4.7), (5.3, -4.4), (-7.8, 6.4), (7.9, 6.0))
        for k, (x, z) in enumerate(patches):
            if T.i == 1 and k % 2:
                continue
            cover(g, T, (x, z), 1.0, (5, 4, 0)[T.i], 1400 + k)
    bushes = ((-6.2, -2.0, 1.2), (6.4, -1.9, 1.15), (-5.3, 4.8, 1.3), (5.6, 5.2, 1.3), (-.6, 8.4, 1.25), (-3.2, -6.4, 1.1),
              (3.6, -6.9, 1.1), (7.4, 4.6, 1.3), (-7.6, 3.6, 1.3))
    for k, (x, z, s) in enumerate(bushes):
        if T.i == 2 and k > 4:
            break
        bush(g, T, (x, z), s, 1250 + k)
    if T.detail:
        for k, (x, z, s) in enumerate(((-5.4, GZ, .6), (4.6, GZ, .65), (6.3, GZ - .1, .55))):
            fern(g, T, (x, z), s, 1300 + k, fronds=(4, 3, 0)[T.i], y=wall_h(x) - .2)


# =============================================================================================================================
# The canopy overhead and the forest beyond
# =============================================================================================================================
def crown(g, T):
    """The canopy: a closed ceiling of leaf (a lumpy sheet facing down at 10-13 m), leaf masses lower round the tree tops
    (coarse faceted lumps) and sprays of big leaves hanging under and round them, so a low camera looking up sees leaves, not
    sky."""
    r = mk.rng(101)
    step = (3.3, 4.6, 7.5)[T.i]
    xs = np.arange(-29.7, 29.7 + 1e-6, step)
    zs = np.arange(-29.7, 29.7 + 1e-6, step)
    V = []
    for z in zs:
        for x in xs:
            n = float(sk.fbm(np.array([[x, 0.0, z]]), .12, 3, 7)[0]) - .5
            rr = max(abs(x), abs(z)) * .7 + math.hypot(x, z) * .3
            V.append((x, 12.2 + 3.2 * n - .022 * max(0.0, rr - 12) ** 2, z))
    nu = len(xs)
    faces = []
    for j in range(len(zs) - 1):
        for i in range(nu - 1):
            cx, cz = (xs[i] + xs[i + 1]) / 2, (zs[j] + zs[j + 1]) / 2
            faces.append((j * nu + i, j * nu + i + 1, (j + 1) * nu + i + 1, (j + 1) * nu + i))
    emit(g, V, faces, F_CROWN_DK, smooth=True, orient=(0, -1, 0))
    masses = [(t[0] + float(r.uniform(-1.5, 1.5)), 9.6 + float(r.uniform(-.6, .8)), t[1] + float(r.uniform(-1.5, 1.5)), 2.8)
              for t in TREES] + [(.4, 9.7, -2.9, 2.4), (-.5, 10.4, 3.4, 2.6), (-8.5, 9.8, -4.5, 3.0), (9.0, 10.0, -4.0, 3.0)]
    for k, (x, y, z, s) in enumerate(masses):
        mk.rock(g, (x, y, z), (s, s * float(r.uniform(.45, .6)), s * float(r.uniform(.8, 1.05))), F_CROWN if k % 2 else F_CROWN_DK,
                T, seed=1400 + k, rough=.32, flat_bottom=False, level=0, facets=T.i < 2, turn=float(r.uniform(0, 3)))
        if T.detail and k < 8:
            for i in range((4, 2, 0)[T.i]):
                a = 2 * math.pi * i / 4 + float(r.uniform(-.4, .4))
                rad = s * float(r.uniform(.45, .95))
                p = (x + math.cos(a) * rad, y - s * float(r.uniform(.15, .4)), z + math.sin(a) * rad)
                spray(g, p, (math.cos(a) * .7, -.8, math.sin(a) * .7), (3, 3, 0)[T.i], float(r.uniform(1.2, 1.7)),
                      float(r.uniform(.45, .6)), (F_LEAF_DK, F_LEAF, F_CROWN), r, spread=.8)


def far(g, T):
    """The forest beyond: coarse trunks and leaf walls in the dark, closing every side."""
    r = mk.rng(111)
    n = (9, 8, 7)[T.i]
    for k in range(n):
        a = 2 * math.pi * k / n + float(r.uniform(-.12, .12))
        d = float(r.uniform(13, 21))
        x, z = math.cos(a) * d, math.sin(a) * d
        rad = float(r.uniform(.5, 1.1))
        h = float(r.uniform(11, 16))
        sk.lathe(g, [(rad * 1.7, 0), (rad, 2.0), (rad * .75, h)], (x, ground_y(x, z) - .3, z), F_FAR, 5)
    for k in range((8, 7, 6)[T.i]):
        a = 2 * math.pi * k / (8, 7, 6)[T.i] + .2
        d = float(r.uniform(18, 26))
        x, z = math.cos(a) * d, math.sin(a) * d
        s = float(r.uniform(7, 10))
        mk.rock(g, (x, float(r.uniform(3.5, 6.5)), z), (s, s * .9, s * .8), F_FAR, T, seed=1500 + k, rough=.35, flat_bottom=False,
                level=0, facets=True, turn=a)



def bush(g, T, at, s=1.0, seed=0, y=None):
    """A clump of undergrowth: a low leafy mound with sprays of big leaves rising out of it and arching over."""
    r = mk.rng(seed)
    x, z = at
    y = ground_y(x, z) if y is None else y
    sz = s * .55
    mk.rock(g, (x, y - sz * .12, z), (sz, sz * .55, sz * .9), F_LEAF_DK, T, seed=seed * 7, rough=.3, flat_bottom=False, level=0,
            facets=False, turn=float(r.uniform(0, 3)))
    ns = (4, 3, 2)[T.i]
    for i in range(ns):
        a = 2 * math.pi * i / ns + float(r.uniform(-.3, .3))
        p = (x + math.cos(a) * sz * .35, y + sz * .25, z + math.sin(a) * sz * .35)
        spray(g, p, (math.cos(a), float(r.uniform(.4, 1.1)), math.sin(a)), (3, 3, 2)[T.i], s * .55, s * .24,
              (F_LEAF, F_LEAF_LT, F_LEAF_DK), r, spread=.6, droop=.25)



def spray(g, base, d, n, L, W, fins, r, spread=.7, droop=.0):
    """A spray of leaves: n leaf cards fanning out from one point round the direction d."""
    d = sk._norm(d)
    side = sk._norm(sk._cross(d, (0, 1, 0))) if abs(d[1]) < .95 else (1.0, 0.0, 0.0)
    up = sk._norm(sk._cross(side, d))
    for i in range(n):
        a = (i / max(1, n - 1) - .5) * 2 * spread + float(r.uniform(-.15, .15))
        b = float(r.uniform(-.35, .35))
        dd = sk._norm(sk._add(sk._add(d, side, math.sin(a)), up, b - droop))
        k = float(r.uniform(.75, 1.15))
        leaf_card(g, sk._add(base, dd, L * .08), dd, L * k, W * k, fins[i % len(fins)], F_LEAF_DK, fold=.3)



def cover(g, T, at, rad, n, seed):
    """Ground cover: a patch of broad leaves lying low over the earth (seedlings, creepers), turned out from its middle."""
    r = mk.rng(seed)
    x0, z0 = at
    for i in range(n):
        a = float(r.uniform(0, 2 * math.pi))
        d = rad * float(r.uniform(.1, 1.0)) ** .6
        x, z = x0 + math.cos(a) * d, z0 + math.sin(a) * d
        y = ground_y(x, z) + .01
        out = a + float(r.uniform(-.6, .6))
        leaf_card(g, (x, y, z), (math.cos(out), float(r.uniform(.15, .55)), math.sin(out)), float(r.uniform(.25, .42)),
                  float(r.uniform(.12, .2)), (F_LEAF_DK, F_FERN_DK, F_LEAF, F_LEAF_Y)[i % 4], F_LEAF_DK, fold=.3)
