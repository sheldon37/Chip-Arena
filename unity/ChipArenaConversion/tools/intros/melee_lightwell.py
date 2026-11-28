"""Severed Dawn's set (the sword; docs/INTROS-MELEE.md): the Lightwell, a ruined vaulted hall at dawn.

A Gothic hall: two rows of massive lathed columns (x = +-3.6, every 3.2 m from z = +6 to -13.2, with responds in the end walls)
carry pointed rib vaults over the nave and the side aisles, all springing at 5.75 m and rising to 10.15 m. Over the bay in front of
the pilot the nave vault is broken open in a jagged cleft centred over z = +0.6 (its broken shell shows its thickness, ivy hangs from
its rim, the diagonal ribs end in broken stubs), so one shaft of dawn falls through it onto the floor just in front of the pilot;
fallen vault stones lie round that spot (outside the 2.5 m clear ring; only grit inside it). The far right corner of the vault has
fallen in over a broken column, its rubble on the floor and the dais. The floor is worn flagstones, cracked, a few gone. The aisles'
outer walls hold tall narrow lancet windows (dark glass, faintly glowing: glow finish at low gain). At the far end (-z) a dais with
broken steps under a great empty rose window, its tracery half fallen; behind the camera's side (+z) the entrance wall and its great
door arch, one door leaf fallen. Iron chains hang from the vault, one with a lopsided lamp hoop; an iron candelabrum lies toppled.
No rigs. Set frame: melee_kit's (the pilot at the origin facing +z; -x is the opening camera's right).
"""
import array
import math

import numpy as np

import melee_kit as mk
from melee_kit import sk

# ---- finishes: a warm grey limestone hall, umber in its shadows ----
F_STONE = mk.fin('lightwell.stone', 0x8b8374, sk.DETAIL_CARVED, smooth=.3, dirt=1.0, moss=.15, wear=.45)
F_STONE_DK = mk.fin('lightwell.stone.dark', 0x6f685c, sk.DETAIL_CARVED, smooth=.3, dirt=1.1, moss=.2, wear=.35)
F_ASHLAR = mk.fin('lightwell.ashlar', 0x857d6f, sk.DETAIL_ASHLAR, smooth=.25, dirt=1.1, moss=.2, wear=.2)
F_VAULT = mk.fin('lightwell.vault', 0x7e7669, sk.DETAIL_ASHLAR, smooth=.2, dirt=1.0, param=.8)
F_VAULT_TOP = mk.fin('lightwell.vault.top', 0x6c6c58, sk.DETAIL_CARVED, smooth=.15, dirt=1.3, moss=1.2)
F_RIB = mk.fin('lightwell.rib', 0x968d7c, sk.DETAIL_CARVED, smooth=.35, dirt=.9, wear=.5)
F_COLUMN = mk.fin('lightwell.column', 0x918878, sk.DETAIL_CARVED, smooth=.4, dirt=1.0, moss=.15, wear=.4)
F_CARVE = mk.fin('lightwell.carving', 0x9b927f, sk.DETAIL_CARVED, smooth=.35, dirt=.9, wear=.55)
F_FLAG = mk.fin('lightwell.flag', 0x7b7366, sk.DETAIL_CARVED, smooth=.5, dirt=1.0, moss=.25, wear=.35)
F_FLAG_DK = mk.fin('lightwell.flag.dark', 0x665f54, sk.DETAIL_CARVED, smooth=.45, dirt=1.1, moss=.3, wear=.3)
F_FLOOR = mk.fin('lightwell.floor', 0x716a5e, sk.DETAIL_FLAG, smooth=.45, dirt=1.0, moss=.2)
F_BED = mk.fin('lightwell.bed', 0x4b4239, smooth=.15, dirt=.8, moss=.4)
F_RUBBLE = mk.fin('lightwell.rubble', 0x7d7567, sk.DETAIL_CARVED, smooth=.25, dirt=1.1, moss=.2, wear=.4)
F_IRON = mk.fin('lightwell.iron', 0x35312d, sk.DETAIL_IRON, smooth=.45, metal=.7, dirt=.6)
F_GLASS = mk.fin('lightwell.glass.glow', 0x5b6d88, glow=.55)
F_LEAD = mk.fin('lightwell.lead', 0x3b3a3a, sk.DETAIL_IRON, smooth=.35, metal=.5, dirt=.5)
F_WOOD = mk.fin('lightwell.wood', 0x4e3d2e, sk.DETAIL_WOOD, smooth=.3, dirt=.8, param=1)
F_WOOD_H = mk.fin('lightwell.wood.h', 0x4e3d2e, sk.DETAIL_WOOD, smooth=.3, dirt=.8, param=0)
F_WAX = mk.fin('lightwell.wax', 0xb5a88c, sk.DETAIL_WAX, smooth=.4, dirt=.5)
F_IVY = mk.fin('lightwell.ivy', 0x4c6131, smooth=.4, dirt=.2)
F_IVY_DK = mk.fin('lightwell.ivy.dark', 0x3b4f2b, smooth=.4, dirt=.2)
F_GRASS = mk.fin('lightwell.grass', 0x5f6b3a, smooth=.3, dirt=.3)
F_OUT = mk.fin('lightwell.outside', 0x5d5a50, smooth=.2, dirt=.5, moss=.4)
F_FAR = mk.fin('lightwell.far', 0x4a4a48, smooth=.2, dirt=0)

COL_X = 3.6             # the arcades' line (x = +-COL_X)
BAY = 3.2
COL_Z = tuple(6.0 - BAY * k for k in range(7))      # the free columns: z = 6.0 .. -13.2
ENT_Z, END_Z = 9.2, -16.4                           # the responds in the entrance and end walls
EDGES = (ENT_Z,) + COL_Z + (END_Z,)                 # the bays' bounds along z
WALL_X = 7.6            # the aisles' outer walls (their inner faces)
ENT_FACE, END_FACE = 9.6, -16.8
SPRING = 5.75           # every vault springs here
RISE = 4.4              # and rises to 10.15 (nave, aisles and arcades alike: a hall church)
THICK = .45             # the vault shell
CLEFT_Z = .6            # the cleft is centred over (0, z = 0.6)
DAIS_Z, DAIS_Y = -14.1, .88
STEP_N, STEP_RUN = 4, .35
ROSE_Y, ROSE_R = 6.9, 2.5
DOOR_L, DOOR_SPRING, DOOR_H = 1.9, 3.8, 2.6
BROKEN_COL = (-COL_X, -13.2)
NEAR_Z0 = -7.0          # modelled flagstones from here to the entrance


def build(T):
    s = mk.SetPiece('lightwell')
    g = mk.Geo()
    tl = mk.Tally(g)
    floor(g, T)
    tl('floor')
    columns(g, T)
    tl('columns')
    vaults(g, T)
    tl('vaults')
    ribs(g, T)
    tl('ribs')
    aisle_walls(g, T)
    tl('aisle walls')
    end_wall(g, T)
    tl('end wall+rose')
    dais(g, T)
    tl('dais')
    entrance(g, T)
    tl('entrance')
    rubble(g, T)
    tl('rubble')
    ironwork(g, T)
    tl('iron')
    outside(g, T)
    tl('outside')
    s.tally = tl
    s.static[T.i] = g
    s.views = [
        ('front', (0.0, 1.5, 6.0), (0.0, 1.4, -3.0), 50),
        ('shaft', (0.3, 0.4, 1.4), (0.0, 8.0, 0.6), 70),
        ('dais', (0.0, 2.7, -14.6), (0.0, 1.8, 6.0), 55),
        ('aisle', (5.6, 1.6, 7.6), (5.4, 2.6, -12.0), 55),
        ('reverse', (0.5, 1.6, -3.5), (0.0, 2.4, 8.0), 55),
        ('high', (2.4, 7.6, 8.4), (0.0, 1.0, -6.0), 62),
        ('cleft', (1.8, 1.7, 3.8), (0.0, 9.5, 0.4), 62),
        ('above', (0.4, 13.5, 2.2), (0.0, 0.0, 0.6), 50),
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


def _t(p):
    return (float(p[0]), float(p[1]), float(p[2]))


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


# =============================================================================================================================
# The hall's geometry: the arches' profiles, the vault's surface, the holes broken in it
# =============================================================================================================================
def t_nave(x):
    return sk.arch_height(COL_X, RISE, COL_X - abs(x))


def t_aisle(x):
    c = math.copysign((COL_X + WALL_X) / 2, x)
    return sk.arch_height((WALL_X - COL_X) / 2, RISE, (WALL_X - COL_X) / 2 - abs(x - c))


def a_bay(z, zc):
    return sk.arch_height(BAY / 2, RISE, BAY / 2 - abs(z - zc))


def vault_y(x, z):
    """The vaults' underside over (x, z)."""
    for k in range(len(EDGES) - 1):
        if EDGES[k + 1] <= z <= EDGES[k]:
            zc = (EDGES[k] + EDGES[k + 1]) / 2
            t = t_nave(x) if abs(x) <= COL_X else t_aisle(x)
            return SPRING + max(t, a_bay(z, zc))
    return SPRING


def in_cleft(x, z):
    """The cleft broken in the vault over the pilot's front: a ragged crack across the nave (about 4.6 m by 1.5 m), centred
    over z = 0.6, a branch of it running off toward the right."""
    if z < -.25:
        return False
    w = .78 * (1 - (x / 2.45) ** 2) ** .5 if abs(x) < 2.45 else 0.0
    w *= 1 + .28 * math.sin(x * 4.1 + .7) + .14 * math.sin(x * 9.3)
    if abs(z - CLEFT_Z - .12 * math.sin(x * 1.9)) < w:
        return True
    # the branch: a narrower crack from the cleft's middle toward -x, +z
    t = (-x - .3) / 1.6
    return 0 < t < 1 and abs(z - (CLEFT_Z + .55 + .9 * t)) < .26 * (1 - t) + .05


def in_collapse(x, z):
    """The far right corner, fallen in over the broken column."""
    return x < -1.7 + .5 * math.sin(z * 2.1) and -15.5 + .4 * math.sin(x * 1.7) < z < -10.7 + .5 * math.sin(x * 2.3)


def holed(x, z):
    return in_cleft(x, z) or in_collapse(x, z)


def arch(A, B, L, H, n):
    """The points of a pointed arch from springer A to springer B (same height), half span L, rise H, n segments a half."""
    half = sk.pointed_arch(L, H, n)
    d = sk._norm(sk._sub(B, A))
    left = [sk._add(sk._add(A, d, dd), (0, 1, 0), hh) for dd, hh in half]
    right = [sk._add(sk._add(B, d, -dd), (0, 1, 0), hh) for dd, hh in half]
    return left + right[::-1][1:]


def groin(xc, zc, Lx, sx, sz, n):
    """A quarter of a bay's diagonal rib, from its corner (sx, sz) up the groin to the crown: where the cross vault (half span
    Lx) meets the bay's own (half span BAY / 2), both rising RISE."""
    return [(xc + sx * (Lx - dd), SPRING + hh, zc + sz * sk.arch_width(BAY / 2, RISE, hh)) for dd, hh in sk.pointed_arch(Lx, RISE, n)]


RIB = ((-.11, -.04), (.11, -.04), (.11, .08), (0.0, .21), (-.11, .08))
RIB_LO = ((-.11, -.04), (.11, -.04), (0.0, .18))
RIB_THIN = ((-.08, -.04), (.08, -.04), (.08, .07), (-.08, .07))


def rib(g, T, pts, centre, f=None, sec=None, seed=0, holes=True):
    """A moulded rib along pts (on the vault's underside), hanging toward `centre`; cut where the vault is holed, each cut end
    a broken lump."""
    f = F_RIB if f is None else f
    sec = sec or (RIB if T.i < 2 else RIB_LO)
    runs, cur = [], []
    for p in pts:
        if holes and holed(p[0], p[2]):
            if len(cur) > 1:
                runs.append(cur)
            cur = []
        else:
            cur.append(p)
    if len(cur) > 1:
        runs.append(cur)
    for k, run in enumerate(runs):
        n = len(run)
        ns = []
        for i in range(n):
            t = sk._norm(sk._sub(run[min(n - 1, i + 1)], run[max(0, i - 1)]))
            v = sk._sub(centre, run[i])
            ns.append(sk._norm(sk._add(v, t, -sk._dot(v, t))))
        sk.sweep(g, sec, run, f, path_normals=ns, caps=True)
        for end, cut in ((run[0], run[0] is not pts[0]), (run[-1], run[-1] is not pts[-1])):
            if cut:
                mk.rock(g, end, (.15, .15, .15), F_RIB, T, seed=seed * 7 + k, rough=.45, flat_bottom=False, level=0, facets=False)


def web(g, T, xa, xb, za, zb, hfun, nx, nz, extrados=False, seed=0):
    """One bay's vault shell over [xa, xb] x [za, zb]: its underside facing down (height SPRING + hfun(x, z)), broken open where
    holed(); round each hole the shell's broken edge (its thickness) and, with extrados, its back facing up."""
    xs = np.linspace(xa, xb, nx)
    zs = np.linspace(za, zb, nz)
    gone = [[holed((xs[i] + xs[i + 1]) / 2, (zs[j] + zs[j + 1]) / 2) for i in range(nx - 1)] for j in range(nz - 1)]
    r = mk.rng(seed)
    P = []
    for j, z in enumerate(zs):
        for i, x in enumerate(xs):
            adj = [gone[jj][ii] for jj in (j - 1, j) for ii in (i - 1, i) if 0 <= jj < nz - 1 and 0 <= ii < nx - 1]
            if any(adj) and not all(adj) and 0 < i < nx - 1 and 0 < j < nz - 1:
                x = x + float(r.uniform(-.45, .45)) * (xs[1] - xs[0])
                z = z + float(r.uniform(-.45, .45)) * (zs[1] - zs[0])
            P.append((x, SPRING + hfun(x, z), z))
    faces = []
    for j in range(nz - 1):
        for i in range(nx - 1):
            if not gone[j][i]:
                faces.append((j * nx + i, j * nx + i + 1, (j + 1) * nx + i + 1, (j + 1) * nx + i))
    emit(g, P, faces, F_VAULT, smooth=True, orient=(0, -1, 0))
    if extrados:
        Pt = [(p[0], p[1] + THICK, p[2]) for p in P]
        emit(g, Pt, faces, F_VAULT_TOP, smooth=True, orient=(0, 1, 0))
    # the broken edges: a band of the shell's thickness wherever a kept cell meets a fallen one
    for j in range(nz - 1):
        for i in range(nx - 1):
            if gone[j][i]:
                continue
            v00, v01, v11, v10 = j * nx + i, j * nx + i + 1, (j + 1) * nx + i + 1, (j + 1) * nx + i
            for (jj, ii), (a, b) in (((j - 1, i), (v00, v01)), ((j + 1, i), (v10, v11)), ((j, i - 1), (v00, v10)), ((j, i + 1), (v01, v11))):
                if 0 <= jj < nz - 1 and 0 <= ii < nx - 1 and gone[jj][ii]:
                    pa, pb = P[a], P[b]
                    mid = ((xs[ii] + xs[ii + 1]) / 2 - (xs[i] + xs[i + 1]) / 2, 0.0, (zs[jj] + zs[jj + 1]) / 2 - (zs[j] + zs[j + 1]) / 2)
                    g.face((pa, pb, (pb[0], pb[1] + THICK, pb[2]), (pa[0], pa[1] + THICK, pa[2])), F_STONE_DK, normal=sk._norm(mid))


def vaults(g, T):
    """The rib vaults' shells over the nave and both aisles, bay by bay (the cleft's bay and the collapsed corner finer, so their
    holes are ragged); the shell's back round the cleft for a camera above it."""
    for k in range(len(EDGES) - 1):
        za, zb = EDGES[k], EDGES[k + 1]
        zc = (za + zb) / 2
        holes = k == 2 or k >= 6
        nx, nz = ((19, 11) if holes else (11, 7), (13, 7) if holes else (7, 5), (9, 5) if holes else (5, 3))[T.i]
        web(g, T, -COL_X, COL_X, za, zb, lambda x, z, zc=zc: max(t_nave(x), a_bay(z, zc)), nx, nz, extrados=1 <= k <= 3 and T.i < 2,
            seed=900 + k)
        for side in (-1, 1):
            ax = ((9, 7) if (holes and side < 0) else (6, 5), (5, 4), (4, 3))[T.i]
            web(g, T, side * COL_X, side * WALL_X, za, zb, lambda x, z, zc=zc: max(t_aisle(x), a_bay(z, zc)), ax[0], ax[1],
                seed=950 + k * 2 + (side > 0))
    if T.detail:
        cleft_rim(g, T)


def cleft_edge(x, sgn):
    """Where the cleft's edge runs at x: its far (+1) or near (-1) lip."""
    w = .78 * max(0.0, 1 - (x / 2.45) ** 2) ** .5 * (1 + .28 * math.sin(x * 4.1 + .7) + .14 * math.sin(x * 9.3))
    return CLEFT_Z + .12 * math.sin(x * 1.9) + sgn * w


def cleft_rim(g, T):
    """Along the cleft's lips: loose stones on the vault's back, grass on them, ivy hanging down into the shaft."""
    r = mk.rng(31)
    n = (18, 10, 0)[T.i]
    for k in range(n):
        sgn = 1 if k % 2 else -1
        x = -2.15 + 4.3 * (k // 2) / max(1, n // 2 - 1) + float(r.uniform(-.12, .12))
        z = cleft_edge(x, sgn) + sgn * float(r.uniform(.1, .22))
        if z < -.3 or holed(x, z):
            continue
        y = SPRING + max(t_nave(x), a_bay(z, 1.2)) + THICK
        s = float(r.uniform(.12, .26))
        mk.rock(g, (x, y - .04, z), (s, s * .6, s * .9), F_RUBBLE, T, seed=3000 + k, rough=.35, turn=float(r.uniform(0, 3)),
                level=0)
        if k % 3 == 0:
            for i in range(3):
                b = float(r.uniform(0, 2 * math.pi))
                leaf_card(g, (x + math.cos(b) * .1, y, z + math.sin(b) * .1), (math.cos(b) * .4, 1.0, math.sin(b) * .4),
                          float(r.uniform(.2, .32)), .05, F_GRASS, F_IVY_DK, fold=.2)
    # ivy hanging from the lips into the shaft
    for k, (x, sgn, L) in enumerate(((-1.6, 1, 2.2), (1.2, -1, 1.6), (-.3, 1, 2.8), (.7, -1, 1.3), (1.9, 1, 1.1))[:(5, 3, 0)[T.i]]):
        z = cleft_edge(x, sgn) + sgn * .04
        top = (x, SPRING + max(t_nave(x), a_bay(z, 1.2)) - .02, z)
        ivy(g, T, top, L, 40 + k)


def ivy(g, T, top, L, seed):
    """A trailing stem of ivy hanging from `top`, leaves along it."""
    r = mk.rng(seed)
    pts = [top]
    for i in range(1, 4):
        t = i / 3
        pts.append((top[0] + float(r.uniform(-.12, .12)) * t, top[1] - L * t, top[2] + float(r.uniform(-.12, .12)) * t))
    sk.tube(g, sk.catmull(pts, 2), .012, F_IVY_DK, 3, caps=False)
    for i in range(int(L / .22)):
        t = (i + .5) / int(L / .22)
        p = (top[0] + (pts[-1][0] - top[0]) * t, top[1] - L * t, top[2] + (pts[-1][2] - top[2]) * t)
        a = float(r.uniform(0, 2 * math.pi))
        leaf_card(g, p, (math.cos(a), float(r.uniform(-.5, .2)), math.sin(a)), float(r.uniform(.09, .14)), float(r.uniform(.08, .12)),
                  F_IVY if i % 2 else F_IVY_DK, F_IVY_DK, fold=.15)


def ribs(g, T):
    """The ribs: transverse arches over the nave and the aisles at every column and respond, the arcade arches between the
    columns, the nave bays' diagonal ribs (and on Fine the aisles' too), all broken off where the vault has fallen."""
    n = (7, 5, 3)[T.i]
    for k, z in enumerate(EDGES):
        rib(g, T, arch((-COL_X, SPRING, z), (COL_X, SPRING, z), COL_X, RISE, n), (0.0, SPRING, z), seed=10 + k)
        for side in (-1, 1):
            rib(g, T, arch((side * COL_X, SPRING, z), (side * WALL_X, SPRING, z), (WALL_X - COL_X) / 2, RISE, n - 1),
                (side * (COL_X + WALL_X) / 2, SPRING, z), sec=RIB_THIN if T.i < 2 else RIB_LO, seed=20 + k * 2 + (side > 0))
    for k in range(len(EDGES) - 1):
        za, zb = EDGES[k], EDGES[k + 1]
        zc = (za + zb) / 2
        for side in (-1, 1):
            rib(g, T, arch((side * COL_X, SPRING, za), (side * COL_X, SPRING, zb), BAY / 2, RISE, max(2, n - 2)), (side * COL_X, SPRING, zc),
                seed=40 + k * 2 + (side > 0))
        for sx, sz in ((1, 1), (1, -1)):
            path = groin(0.0, zc, COL_X, sx, sz, n) + groin(0.0, zc, COL_X, -sx, -sz, n)[::-1][1:]
            rib(g, T, path, (0.0, SPRING + 1.0, zc), seed=60 + k * 2 + (sx * sz > 0))
        # a boss where the nave's diagonals cross
        if not holed(0.0, zc):
            mk.rock(g, (0.0, SPRING + RISE - .16, zc), (.2, .14, .2), F_CARVE, T, seed=70 + k, rough=.2, flat_bottom=False,
                    level=0, facets=False)


# =============================================================================================================================
# Columns, responds, walls and windows
# =============================================================================================================================
def columns(g, T):
    """The two arcades' columns: a square plinth, an attic base, a slightly tapering shaft, a bell capital under a square
    abacus (leafy knobs round the bell on Fine); the right one at the dais broken off, its drums and capital fallen."""
    for side in (-1, 1):
        for k, z in enumerate(COL_Z):
            x = side * COL_X
            near = z > -8
            sides = ((12 if near else 9), 8, 6)[T.i]
            broken = (x, z) == BROKEN_COL
            column(g, T, x, z, 100 + k * 2 + (side > 0), sides, broken=broken, crockets=near and T.fine,
                   shaken=(x, z) == (-COL_X, -10.0) or (x, z) == (COL_X, -3.6))
    # responds: pilasters in the outer walls and the end walls, with plain capitals
    for z in EDGES:
        for side in (-1, 1):
            respond(g, T, side * (WALL_X - .14), z, (.14, .3), 200 + int(abs(z) * 10) + (side > 0))
    for z, sz in ((ENT_Z, 1), (END_Z, -1)):
        for side in (-1, 1):
            respond(g, T, side * COL_X, z + sz * .26, (.34, .16), 300 + (side > 0) + (z > 0) * 2, y0=DAIS_Y if z < 0 else 0.0)


def column(g, T, x, z, seed, sides, broken=False, crockets=False, shaken=False):
    r = mk.rng(seed)
    rbox(g, T, (x, .14, z), (.8, .17, .8), F_STONE_DK, seed=seed, b=.04, jit=.012, chop=.07 if r.uniform() < .5 else 0.0,
         q=qe(yaw=float(r.uniform(-.02, .02))))
    base = [(.75, .31), (.76, .39), (.65, .5), (.59, .6), (.565, .66)] if T.i < 2 else [(.75, .31), (.7, .45), (.565, .66)]
    sk.lathe(g, base, (x, 0, z), F_COLUMN, sides)
    if broken:
        sk.lathe(g, [(.565, .66), (.56, 1.8), (.552, 2.75)], (x, 0, z), F_COLUMN, sides)
        broken_top(g, T, (x, 2.75, z), .552, sides, seed)
        return
    if shaken and T.i < 2:
        # shaken: its shaft in three drums, the middle one knocked out of true
        sk.lathe(g, [(.565, .66), (.558, 2.1)], (x, 0, z), F_COLUMN, sides, cap1=True)
        dg = mk.Geo()
        sk.lathe(dg, [(.558, 0.0), (.545, 1.55)], (0, 0, 0), F_COLUMN, sides, cap0=True, cap1=True)
        g.extend(dg.transformed(sk.affine(qe(roll=.025, yaw=.3), (x + .07, 2.11, z - .04))))
        sk.lathe(g, [(.545, 3.67), (.535, 4.95)], (x, 0, z), F_COLUMN, sides, cap0=True)
    else:
        sk.lathe(g, [(.565, .66), (.555, 2.6), (.535, 4.95)] if T.i < 2 else [(.565, .66), (.535, 4.95)], (x, 0, z), F_COLUMN, sides)
    cap = [(.535, 4.95), (.585, 5.02), (.6, 5.25), (.74, 5.42), (.8, 5.5)] if T.i < 2 else [(.535, 4.95), (.65, 5.3), (.8, 5.5)]
    sk.lathe(g, cap, (x, 0, z), F_CARVE, sides)
    rbox(g, T, (x, 5.625, z), (.88, .125, .88), F_STONE, seed=seed + 1, b=.035, jit=.01, chop=.06 if r.uniform() < .3 else 0.0)
    if crockets:
        for k in range(6):
            a = 2 * math.pi * k / 6 + math.pi / 6
            mk.rock(g, (x + math.cos(a) * .74, 5.38, z + math.sin(a) * .74), (.1, .075, .1), F_CARVE, T, seed=seed * 9 + k,
                    rough=.3, flat_bottom=False, level=0, facets=False)


def broken_top(g, T, c, rad, sides, seed):
    """A column's snapped top: a ragged rim and a rough break across it."""
    r = mk.rng(seed + 77)
    x, y, z = c
    V = []
    ring0, ring1, ring2 = [], [], []
    for k in range(sides):
        a = 2 * math.pi * k / sides
        ring0.append(len(V))
        V.append((x + math.cos(a) * rad, y, z + math.sin(a) * rad))
    for k in range(sides):
        a = 2 * math.pi * k / sides
        h = float(r.uniform(0, .42)) * (.6 + .4 * math.cos(a - 1.1))
        ring1.append(len(V))
        V.append((x + math.cos(a) * rad * float(r.uniform(.9, 1.0)), y + max(.02, h), z + math.sin(a) * rad * float(r.uniform(.9, 1.0))))
    for k in range(sides):
        a = 2 * math.pi * k / sides
        ring2.append(len(V))
        V.append((x + math.cos(a) * rad * .45, y + float(r.uniform(.05, .3)), z + math.sin(a) * rad * .45))
    top = len(V)
    V.append((x, y + .18, z))
    faces = []
    for k in range(sides):
        k1 = (k + 1) % sides
        faces.append((ring0[k], ring0[k1], ring1[k1], ring1[k]))
        faces.append((ring1[k], ring1[k1], ring2[k1], ring2[k]))
        faces.append((ring2[k], ring2[k1], top))
    emit(g, V, faces, F_COLUMN, smooth=False, orient='volume')


def respond(g, T, x, z, half, seed, y0=0.0):
    """A pilaster set in a wall (half extents across x and z), its plain capital at the springing."""
    hx, hz = half
    if T.i == 2:
        rbox(g, T, (x, (y0 + SPRING) / 2, z), (hx, (SPRING - y0) / 2, hz), F_STONE, seed=seed, b=.03, jit=.01)
        return
    rbox(g, T, (x, (y0 + 5.5) / 2, z), (hx, (5.5 - y0) / 2, hz), F_STONE, seed=seed, b=.03, jit=.01)
    rbox(g, T, (x, 5.625, z), (hx + .08, .125, hz + .08), F_CARVE, seed=seed + 1, b=.03, jit=.008)


def wquad(g, x, z0, z1, y0, y1, f, nrm, cell):
    """A face in the plane x = const (normal nrm = (+-1, 0, 0))."""
    sk.grid_face(g, (x, y0, z0), (0, 0, z1 - z0), (0, y1 - y0, 0), 0, 0, f, nrm, cell if cell > 0 else 99.0)


def aisle_walls(g, T):
    """The aisles' outer walls: ashlar, a tall lancet window in every bay (its reveal, a mullion and a transom, dark glass
    faintly glowing), a sill course under the windows."""
    top = 10.6
    cell = (1.6, 3.2, 99.0)[T.i]
    for side in (-1, 1):
        x = side * WALL_X
        nrm = (-side, 0, 0)
        for k in range(len(EDGES) - 1):
            za, zb = EDGES[k], EDGES[k + 1]
            zc = (za + zb) / 2
            broken = side < 0 and k >= 6
            lancet(g, T, x, side, zc, za, zb, top, cell, broken)
        # the sill course along the wall
        sk.box(g, x - side * .1, x + side * .02, 1.72, 1.86, END_FACE, ENT_FACE, F_STONE, T, bevel=.02, cell=(2.0, 4.0, 0)[T.i],
               skip=('+x',) if side > 0 else ('-x',))


def lancet(g, T, x, side, zc, za, zb, top, cell, broken=False):
    nrm = (-side, 0, 0)
    w, sill, spring, rise = .5, 1.86, 5.0, 1.25
    z0, z1 = zc - w, zc + w
    lo, hi = min(za, zb), max(za, zb)
    if T.i == 2:
        wquad(g, x, lo, hi, 0.0, top, F_ASHLAR, nrm, 0)
        pts = [(x - side * .01, sill, z0)] + [(x - side * .01, p[1], p[2]) for p in arch((x, spring, z0), (x, spring, z1), w, rise, 3)] + [(x - side * .01, sill, z1)]
        g.face(pts, F_GLASS, normal=nrm)
        return
    wquad(g, x, lo, z0, 0.0, top, F_ASHLAR, nrm, cell)
    wquad(g, x, z1, hi, 0.0, top, F_ASHLAR, nrm, cell)
    wquad(g, x, z0, z1, 0.0, sill, F_ASHLAR, nrm, 0)
    head = arch((x, spring, z0), (x, spring, z1), w, rise, (5, 3, 2)[T.i])
    for a, b in zip(head[:-1], head[1:]):
        g.face((a, b, (x, top, b[2]), (x, top, a[2])), F_ASHLAR, normal=nrm)
    # the reveal: the opening's outline carried back through the wall
    outline = [(x, sill, z0), (x, spring, z0)] + head[1:-1] + [(x, spring, z1), (x, sill, z1)]
    depth = .5
    for a, b in zip(outline[:-1] + [outline[-1]], outline[1:] + [outline[0]]):
        mid = ((a[1] + b[1]) / 2, (a[2] + b[2]) / 2)
        inward = sk._norm((0.0, 3.4 - mid[0], zc - mid[1]))
        g.face((a, b, (b[0] + side * depth, b[1], b[2]), (a[0] + side * depth, a[1], a[2])), F_STONE, normal=inward)
    # the glass, set back in the reveal, and its leading: a mullion and a transom
    xg = x + side * (depth - .08)
    if broken:
        pane = [(xg, sill, z0), (xg, 3.1, z0), (xg, 2.6, zc + .1), (xg, sill, z1)]
        g.face(pane, F_GLASS, normal=nrm)
    else:
        g.face([(xg, sill, z0)] + [(xg, p[1], p[2]) for p in head] + [(xg, sill, z1)], F_GLASS, normal=nrm)
        xm = xg - side * .03
        sk.box(g, xm - .02, xm + .02, sill, spring + rise - .05, zc - .035, zc + .035, F_STONE, T, bevel=.01, cell=0)
        sk.box(g, xm - .02, xm + .02, 3.4, 3.47, z0, z1, F_LEAD, T, bevel=0, cell=0)


def end_wall(g, T):
    """The end wall (-z): ashlar round a great round opening, its moulded frame, the tracery half fallen (an inner ring, spokes,
    small round lights; the ones over the collapse gone), its broken pieces on the dais."""
    z = END_FACE
    sq = 2.9
    top = 11.4
    nrm = (0, 0, 1)
    cell = (1.6, 3.2, 99.0)[T.i]
    sk.grid_face(g, (-WALL_X, 0.0, z), (WALL_X - sq, 0, 0), (0, top, 0), 0, 0, F_ASHLAR, nrm, cell)
    sk.grid_face(g, (sq, 0.0, z), (WALL_X - sq, 0, 0), (0, top, 0), 0, 0, F_ASHLAR, nrm, cell)
    sk.grid_face(g, (-sq, 0.0, z), (2 * sq, 0, 0), (0, ROSE_Y - sq, 0), 0, 0, F_ASHLAR, nrm, cell)
    sk.grid_face(g, (-sq, ROSE_Y + sq, z), (2 * sq, 0, 0), (0, top - ROSE_Y - sq, 0), 0, 0, F_ASHLAR, nrm, cell)
    n = (32, 16, 8)[T.i]
    circ = []
    for i in range(n):
        a = 2 * math.pi * i / n
        c, s = math.cos(a), math.sin(a)
        k = sq / max(abs(c), abs(s))
        circ.append(((ROSE_R * c, ROSE_Y + ROSE_R * s), (k * c, ROSE_Y + k * s)))
    for i in range(n):
        (c0, s0), (c1, s1) = circ[i], circ[(i + 1) % n]
        g.face(((c0[0], c0[1], z), (c1[0], c1[1], z), (s1[0], s1[1], z), (s0[0], s0[1], z)), F_ASHLAR, normal=nrm)
        # the reveal through the wall, facing the opening's axis
        mid = ((c0[0] + c1[0]) / 2, (c0[1] + c1[1]) / 2 - ROSE_Y)
        g.face(((c0[0], c0[1], z), (c1[0], c1[1], z), (c1[0], c1[1], z - 1.1), (c0[0], c0[1], z - 1.1)), F_STONE,
               normal=sk._norm((-mid[0], -mid[1], 0)))
    if T.i == 2:
        return
    ring = [(math.cos(a) * (ROSE_R + .08), ROSE_Y + math.sin(a) * (ROSE_R + .08), z + .02) for a in np.linspace(0, 2 * math.pi, n, endpoint=False)]
    sk.sweep(g, ((-.16, -.04), (.16, -.04), (.16, .06), (.06, .16), (-.06, .16), (-.16, .06)), ring, F_CARVE, closed=True,
             path_normals=[(0, 0, 1)] * n)
    # the tracery, set back in the opening
    zt = z - .45
    inner = [(math.cos(a) * .85, ROSE_Y + math.sin(a) * .85, zt) for a in np.linspace(0, 2 * math.pi, (16, 10, 6)[T.i], endpoint=False)]
    sk.sweep(g, sk.rect_section(.15, .22), inner, F_CARVE, closed=True, path_normals=[(0, 0, 1)] * len(inner))
    r = mk.rng(81)
    for k in range(12):
        a = 2 * math.pi * k / 12
        c, s = math.cos(a), math.sin(a)
        if c < -.3 and s < .6 and k % 2 == 0:
            continue                    # gone, on the collapsed side
        r1 = ROSE_R - .02
        if c < -.2 and k % 2 == 1:
            r1 = float(r.uniform(1.3, 1.9))     # snapped
        L = r1 - .92
        mid = (.92 + r1) / 2
        rbox(g, T, (c * mid, ROSE_Y + s * mid, zt), (.065, L / 2, .1), F_CARVE, seed=820 + k, b=.02, jit=.012,
             chop=.05 if r1 < ROSE_R - .1 else 0.0, q=sk.quat_axis(0, 0, 1, a - math.pi / 2))
        # a small round light between this spoke and the next
        b = a + math.pi / 12
        if math.cos(b) < -.1 and k % 3 != 0:
            continue
        cx, cy = math.cos(b) * 1.72, ROSE_Y + math.sin(b) * 1.72
        oc = [(cx + math.cos(t) * .3, cy + math.sin(t) * .3, zt) for t in np.linspace(0, 2 * math.pi, (10, 7, 5)[T.i], endpoint=False)]
        sk.sweep(g, sk.rect_section(.07, .16), oc, F_CARVE, closed=True, path_normals=[(0, 0, 1)] * len(oc))


def dais(g, T):
    """The dais at the far end: a platform of big slabs over masonry, four steps up to it (one block cracked and slid, a piece
    gone), an altar table on it, its slab broken; fallen tracery and vault stones lying on it."""
    r = mk.rng(91)
    zf = DAIS_Z
    rbox(g, T, (0.0, (DAIS_Y - .3) / 2 - .02, (zf + END_FACE) / 2), (4.4, (DAIS_Y + .3) / 2 - .04, (zf - END_FACE) / 2), F_STONE_DK,
         seed=901, b=.04, jit=.02)
    # its top: big slabs, a little uneven
    for i, (x0, x1) in enumerate(((-4.4, -2.3), (-2.3, -.2), (-.2, 2.0), (2.0, 4.4))):
        for j, (za, zb) in enumerate(((zf, zf - 1.3), (zf - 1.3, END_FACE))):
            if (i, j) == (0, 1):
                continue        # under the fallen corner's rubble
            jj = float(r.uniform(-.01, .012))
            mk.slab(g, [(x0 + .015, za - .015), (x1 - .015, za - .015), (x1 - .015, zb + .015), (x0 + .015, zb + .015)], DAIS_Y - .1,
                    DAIS_Y + jj, F_FLAG if (i + j) % 2 else F_FLAG_DK, T, bevel=.03)
    # the steps
    for s in range(STEP_N):
        zr = zf + (STEP_N - s) * STEP_RUN
        top = (s + 1) * DAIS_Y / STEP_N
        x = -2.75
        k = 0
        while x < 2.7:
            L = float(r.uniform(1.4, 2.6)) if T.i < 2 else 5.5
            x1 = min(2.75, x + L)
            if 2.75 - x1 < .6:
                x1 = 2.75
            slid = s == 1 and k == 1
            gone = s == 2 and k == 0 and T.i < 2
            if not gone:
                rbox(g, T, ((x + x1) / 2, (top - .25) / 2 + (.04 if slid else 0), zr - STEP_RUN * .5 + (.12 if slid else 0) - .3),
                     ((x1 - x) / 2 - .012, (top + .25) / 2, STEP_RUN * .5 + .3), F_STONE if (s + k) % 2 else F_STONE_DK,
                     seed=910 + s * 5 + k, b=.04, jit=.025, chop=.1 if r.uniform() < .5 else 0.0,
                     q=qe(yaw=.06 if slid else float(r.uniform(-.01, .01)), roll=.03 if slid else 0.0))
            x = x1
            k += 1
    # the altar table: a slab on two blocks, the slab cracked in two, one half slid down
    zt = -15.6
    for sx in (-1, 1):
        rbox(g, T, (sx * .75, DAIS_Y + .42, zt), (.22, .42, .42), F_STONE, seed=930 + sx, b=.03, jit=.015, chop=.06)
    rbox(g, T, (.55, DAIS_Y + .91, zt), (.68, .08, .55), F_CARVE, seed=933, b=.03, jit=.01, q=qe(yaw=.03))
    rbox(g, T, (-.85, DAIS_Y + .55, zt + .15), (.66, .08, .55), F_CARVE, seed=934, b=.03, jit=.015, chop=.08,
         q=qe(roll=-.62, yaw=-.15))


def entrance(g, T):
    """The entrance wall behind the camera's side (+z): ashlar round a great pointed door arch with three moulded orders on
    jamb shafts, the doorway's reveal, one oak leaf fallen in, the other hanging open; the dawn outside."""
    z = ENT_FACE
    top = 11.4
    nrm = (0, 0, -1)
    cell = (1.6, 3.2, 99.0)[T.i]
    L = DOOR_L
    sk.grid_face(g, (-WALL_X, 0.0, z), (WALL_X - L, 0, 0), (0, top, 0), 0, 0, F_ASHLAR, nrm, cell)
    sk.grid_face(g, (L, 0.0, z), (WALL_X - L, 0, 0), (0, top, 0), 0, 0, F_ASHLAR, nrm, cell)
    n = (8, 5, 3)[T.i]
    head = arch((-L, DOOR_SPRING, z), (L, DOOR_SPRING, z), L, DOOR_H, n)
    for a, b in zip(head[:-1], head[1:]):
        g.face((a, b, (b[0], top, z), (a[0], top, z)), F_ASHLAR, normal=nrm)
    outline = [(-L, 0.0, z), (-L, DOOR_SPRING, z)] + head[1:-1] + [(L, DOOR_SPRING, z), (L, 0.0, z)]
    depth = 1.3
    for a, b in zip(outline[:-1], outline[1:]):
        mid = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
        inward = sk._norm((-mid[0], DOOR_SPRING - .8 - mid[1], 0.0))
        g.face((a, b, (b[0], b[1], b[2] + depth), (a[0], a[1], a[2] + depth)), F_STONE, normal=inward)
    if T.i == 2:
        return
    # three orders of mouldings round the arch, each on a pair of jamb shafts
    for k in range(3):
        off = .2 + .26 * k
        path = arch((-L - off, DOOR_SPRING, z - .08), (L + off, DOOR_SPRING, z - .08), L + off, DOOR_H + off, n + 1)
        sk.sweep(g, ((-.1, -.06), (.1, -.06), (.1, .04), (0.0, .13), (-.1, .04)), path, F_CARVE, caps=True,
                 path_normals=[(0, 0, -1)] * len(path))
        for sx in (-1, 1):
            xs = sx * (L + off)
            sk.lathe(g, [(.09, 0.0), (.09, .25), (.075, .32), (.075, DOOR_SPRING - .25), (.11, DOOR_SPRING - .12), (.12, DOOR_SPRING)],
                     (xs, 0.0, z - .1), F_COLUMN, (8, 6, 4)[T.i])
    # the door leaves: one fallen flat inside the doorway, one still hung, swung open against the reveal
    leaf = mk.Geo()
    sk.box(leaf, 0, 1.85, 0, 4.6, -.06, .06, F_WOOD, T, bevel=.012, cell=0)
    for y in (.6, 2.2, 3.8):
        sk.box(leaf, .05, 1.8, y, y + .09, .06, .08, F_IRON, T, bevel=0, cell=0)
    g.extend(leaf.transformed(sk.affine(qe(yaw=.25, pitch=-math.pi / 2 + .05), (-1.7, .1, z - .4))))
    g.extend(leaf.transformed(sk.affine(qe(yaw=1.92), (L - .05, .02, z + .25))))
    # the threshold under the doorway
    rbox(g, T, (0.0, -.07, z + depth / 2), (L + .05, .08, depth / 2), F_STONE, seed=1700, b=.03, jit=.01)


def floor(g, T):
    """The floor: the nave's near half laid with big worn flagstones (modelled: some cracked, a corner broken here and there,
    three gone and their bed showing); the far nave and the aisles a flagged floor in the shader's pattern."""
    r = mk.rng(12)
    missing = ((-2.1, 3.7), (1.9, -4.7), (2.5, 6.9), (-1.2, -6.2))
    z = ENT_FACE - .02
    while z > NEAR_Z0:
        depth = float(r.uniform(.85, 1.2))
        z0 = max(NEAR_Z0, z - depth)
        x = -COL_X - float(r.uniform(0, .5))
        while x < COL_X:
            w = float(r.uniform(.95, 1.55))
            x1 = min(COL_X + .4, x + w)
            cx, cz = (x + x1) / 2, (z0 + z) / 2
            xa, xb = max(x, -COL_X - .4), min(x1, COL_X + .4)
            near = math.hypot(cx, cz) < 3.4
            gone = any(abs(cx - mx) < .7 and abs(cz - mz) < .6 for mx, mz in missing)
            if gone or xb - xa < .3 or z - z0 < .3:
                if gone and T.i < 2:
                    g.face([(xa, -.07, z0), (xb, -.07, z0), (xb, -.07, z), (xa, -.07, z)], F_BED, normal=(0, 1, 0))
                    for i in range(3):
                        mk.rock(g, (float(r.uniform(xa + .15, xb - .15)), -.07, float(r.uniform(z0 + .15, z - .15))),
                                (.08, .05, .07), F_RUBBLE, T, seed=1600 + i + int(abs(cx) * 10), rough=.3, level=0, facets=False)
                    tufts(g, T, ((xa + xb) / 2, (z0 + z) / 2), (xb - xa) * .35, (4, 3, 0)[T.i], 1650 + int(abs(cx) * 10), y=-.07)
                x = x1
                continue
            gap = .01 if near else float(r.uniform(.012, .03))
            j1, j2 = float(r.uniform(-.02, .02)), float(r.uniform(-.02, .02))
            poly = [(xa + gap, z0 + gap + j1), (xb - gap, z0 + gap + j2), (xb - gap, z - gap - j2 * .5), (xa + gap, z - gap - j1 * .5)]
            if r.uniform() < (.12 if near else .3):
                k = int(r.integers(0, 4))
                a, b, c = poly[k - 1], poly[k], poly[(k + 1) % 4]
                t1, t2 = float(r.uniform(.1, .3)), float(r.uniform(.1, .3))
                poly = poly[:k] + [(b[0] + (a[0] - b[0]) * t1, b[1] + (a[1] - b[1]) * t1), (b[0] + (c[0] - b[0]) * t2, b[1] + (c[1] - b[1]) * t2)] + poly[k + 1:]
            pieces = [poly]
            if r.uniform() < (.2 if near else .3) and xb - xa > .5 and z - z0 > .5:
                pts = [(float(r.uniform(xa + .15, xb - .15)), float(r.uniform(z0 + .15, z - .15))) for _ in range(2)]
                pieces = [mk.shrink(c, .008) for c in mk.voronoi_cells(pts, poly) if len(c) >= 3]
            fl = F_FLAG if r.uniform() < .65 else F_FLAG_DK
            for pc in pieces:
                pcx = sum(p[0] for p in pc) / len(pc)
                pcz = sum(p[1] for p in pc) / len(pc)
                near_pc = min(math.hypot(p[0], p[1]) for p in pc) < 2.8
                top = float(r.uniform(-.006, .003)) if near_pc else float(r.uniform(-.025, .01))
                ang = float(r.uniform(0, .01)) if near_pc else float(r.uniform(0, .035))
                ax = (float(r.uniform(-1, 1)), 0.0, float(r.uniform(-1, 1)))
                tilt = ((pcx, top, pcz), ax, ang) if abs(ax[0]) + abs(ax[2]) > 1e-3 else None
                if T.i == 2 or min(math.hypot(p[0], p[1]) for p in pc) > 5.0:
                    # far off (and on Low) a stone's top only: its sides show in no joint from there
                    t = mk.Geo()
                    t.face([(p[0], top, p[1]) for p in pc], fl, normal=(0, 1, 0))
                    if tilt is not None and T.i < 2:
                        t = t.transformed(mk.about(tilt[0], sk.quat_axis(tilt[1][0], tilt[1][1], tilt[1][2], tilt[2])))
                    g.extend(t)
                else:
                    mk.slab(g, pc, top - .1, top, fl, T, bevel=.035, tilt=tilt)
            x = x1
        z = z0
    # the bed under the modelled stones (it shows in their joints)
    sk.box(g, -COL_X - .4, COL_X + .4, -.2, -.06, NEAR_Z0, ENT_FACE, F_BED, T, bevel=0, cell=(2.0, 4.0, 0)[T.i], skip=('-y',))
    # the far nave and the aisles: a flagged floor
    fc = (2.0, 4.0, 99.0)[T.i]
    sk.grid_face(g, (-COL_X - .4, -.004, END_FACE), (2 * COL_X + .8, 0, 0), (0, 0, NEAR_Z0 - END_FACE), 0, 0, F_FLOOR, (0, 1, 0), fc)
    for side in (-1, 1):
        xa, xb = sorted((side * (COL_X + .4), side * WALL_X))
        sk.grid_face(g, (xa, -.004, END_FACE), (xb - xa, 0, 0), (0, 0, ENT_FACE - END_FACE), 0, 0, F_FLOOR, (0, 1, 0), fc)
    # a few loose slabs on the shader's floor
    for k, (x, z, w, d, yaw) in enumerate(((-5.4, 3.4, 1.0, .9, .08), (5.8, -2.2, 1.1, .8, -.12), (-2.2, -9.4, 1.2, .9, .1),
                                           (1.6, -11.2, 1.0, .85, -.06), (5.2, -9.6, 1.1, .9, .15))):
        if T.i == 2:
            break
        ca, sa = math.cos(yaw), math.sin(yaw)
        poly = [(x + (px * ca - pz * sa), z + (px * sa + pz * ca)) for px, pz in ((-w / 2, -d / 2), (w / 2, -d / 2), (w / 2, d / 2), (-w / 2, d / 2))]
        mk.slab(g, poly, -.08, .025, F_FLAG_DK, T, bevel=.03, tilt=((x, 0.0, z), (1.0, 0.0, .4), .05))


def rubble(g, T):
    """Fallen vault stones: round the light's spot (outside the clear ring, grit inside it), a broken rib on the floor, the
    mound under the fallen corner with the broken column's drums and its capital."""
    r = mk.rng(101)
    # the cleft's stones round the shaft's spot
    spots = ((2.75, 1.7, .32), (3.1, .2, .26), (2.6, 2.6, .22), (-2.85, 1.2, .34), (-3.2, -.6, .24), (-2.5, 2.9, .2), (.9, 3.4, .28),
             (-1.4, 3.3, .2), (3.3, 3.2, .18), (-3.4, 2.4, .26))
    for k, (x, z, s) in enumerate(spots):
        if T.i == 2 and k > 5:
            break
        if k % 2:
            rbox(g, T, (x, s * .55, z), (s * 1.2, s * .6, s * .8), F_RUBBLE, seed=1100 + k, b=.04, jit=.03, chop=.12,
                 q=qe(yaw=float(r.uniform(0, 3)), roll=float(r.uniform(-.4, .4))))
        else:
            mk.rock(g, (x, -.02, z), (s * 1.1, s * .75, s), F_RUBBLE, T, seed=1100 + k, rough=.32, turn=float(r.uniform(0, 3)),
                    level=0)
    # grit and chips in the light (none over 4 cm)
    if T.detail:
        for k in range((14, 7, 0)[T.i]):
            a = float(r.uniform(0, 2 * math.pi))
            d = float(r.uniform(.6, 2.4))
            s = float(r.uniform(.02, .045))
            mk.rock(g, (math.cos(a) * d, -.005, CLEFT_Z * .5 + math.sin(a) * d), (s * 1.3, s * .7, s), F_RUBBLE, T, seed=1150 + k,
                    rough=.3, level=0, facets=False)
    # a broken length of rib on the floor
    piece = arch((-1.5, 0.0, 0.0), (1.5, 0.0, 0.0), 1.5, 1.7, (4, 3, 2)[T.i])[1:5]
    pg = mk.Geo()
    rib(pg, T, piece, (0.0, -1.0, 0.0), holes=False)
    g.extend(pg.transformed(sk.affine(qe(yaw=.9, pitch=-math.pi / 2 + .15), (3.3, .12, -2.4))))
    # weeds where the rain comes in under the fallen corner and along the walls' feet
    if T.detail:
        for k, (x, z, rad) in enumerate(((-5.6, -12.4, .9), (-4.2, -14.6, .6), (-6.8, -10.6, .7), (-2.6, -11.2, .5), (6.9, -4.0, .5),
                                         (-7.0, 2.2, .5), (7.0, 5.6, .4))):
            tufts(g, T, (x, z), rad, (5, 3, 0)[T.i], 1700 + k, y=DAIS_Y if (abs(x) < 4.4 and z < DAIS_Z) else 0.0)
    # the collapse: a mound of vault stones and rib pieces under the fallen corner, the column's drums and capital
    for k in range((18, 10, 5)[T.i]):
        x = float(r.uniform(-7.0, -1.8))
        z = float(r.uniform(-15.4, -11.0))
        s = float(r.uniform(.18, .45))
        y = DAIS_Y if (abs(x) < 4.4 and z < DAIS_Z) else 0.0
        if k % 3:
            mk.rock(g, (x, y - .03, z), (s * 1.2, s * .7, s), F_RUBBLE, T, seed=1200 + k, rough=.32, turn=float(r.uniform(0, 3)),
                    level=0)
        else:
            rbox(g, T, (x, y + s * .4, z), (s * 1.3, s * .5, s * .9), F_RUBBLE, seed=1200 + k, b=.04, jit=.04, chop=.12,
                 q=qe(yaw=float(r.uniform(0, 3)), roll=float(r.uniform(-.5, .5)), pitch=float(r.uniform(-.3, .3))))
    bx, bz = BROKEN_COL
    sides = (10, 8, 6)[T.i]
    for k, (cx, cz, yaw, L) in enumerate(((-1.7, -11.3, .4, 1.05), (-2.9, -10.5, -.7, .95))):
        dg = mk.Geo()
        sk.lathe(dg, [(.55, -L / 2), (.555, L / 2)], (0, 0, 0), F_COLUMN, sides, cap0=True, cap1=True)
        g.extend(dg.transformed(sk.affine(qe(yaw=yaw, roll=math.pi / 2), (cx, .53, cz))))
    cg = mk.Geo()
    sk.lathe(cg, [(.535, 0.0), (.585, .06), (.56, .15), (.62, .33), (.74, .48), (.8, .55)], (0, 0, 0), F_CARVE, sides, cap0=True)
    rbox(cg, T, (0, .675, 0), (.88, .125, .88), F_STONE, seed=1290, b=.035, jit=.02, chop=.12)
    g.extend(cg.transformed(sk.affine(qe(yaw=.3, roll=2.6), (-2.5, .6, -12.0))))


def chain(g, T, top, bottom, seed, sway=.12):
    """An iron chain hung from `top`: a twisted cord whose section turns a quarter at every link (it reads as links)."""
    r = mk.rng(seed)
    L = top[1] - bottom[1]
    n = max(2, int(L / .14))
    pts, nrms, sc = [], [], []
    for i in range(n + 1):
        t = i / n
        pts.append((top[0] + (bottom[0] - top[0]) * t + sway * math.sin(t * 2.3 + seed) * t, top[1] - L * t,
                    top[2] + (bottom[2] - top[2]) * t + sway * .6 * math.cos(t * 1.9 + seed) * t))
        a = (i % 2) * math.pi / 2
        nrms.append((math.cos(a), 0.0, math.sin(a)))
        sc.append(1.0 if i % 2 else .55)
    sk.sweep(g, sk.rect_section(.05, .016), pts, F_IRON, path_normals=nrms, scale=sc, caps=False)
    return pts[-1]


def ironwork(g, T):
    """Chains from the vault (one with a lamp hoop hanging lopsided), an iron candelabrum toppled on the floor, its candles
    spilled."""
    if T.i == 2:
        for k, (x, z, yb) in enumerate(((-2.2, -2.5, 4.3), (2.0, -6.4, 3.6))):
            chain(g, T, (x, SPRING + RISE - .3, z), (x, yb, z), 500 + k, sway=0.0)
        return
    hooks = ((-2.25, -2.5, 4.3), (2.0, -6.4, 3.7), (-1.3, -9.5, 5.4), (2.6, 4.2, 5.2), (-2.9, 6.6, 6.0))
    ends = []
    for k, (x, z, yb) in enumerate(hooks):
        ends.append(chain(g, T, (x, vault_y(x, z) - .03, z), (x, yb, z), 500 + k))
    # the lamp hoop on the second chain, hanging lopsided from three short chains
    ex, ey, ez = ends[1]
    hoop = mk.Geo()
    ring = [(math.cos(a) * .55, 0.0, math.sin(a) * .55) for a in np.linspace(0, 2 * math.pi, (14, 9, 6)[T.i], endpoint=False)]
    sk.sweep(hoop, sk.rect_section(.03, .07), ring, F_IRON, closed=True, path_normals=[(0, 1, 0)] * len(ring))
    for k in range(6):
        a = 2 * math.pi * k / 6
        sk.lathe(hoop, [(.0, -.02), (.05, 0.0), (.045, .06)], (math.cos(a) * .55, .035, math.sin(a) * .55), F_IRON, 5)
        if k % 2 == 0:
            sk.lathe(hoop, [(.025, .06), (.025, .16 + .06 * k / 2), (.0, .17 + .06 * k / 2)], (math.cos(a) * .55, 0.0, math.sin(a) * .55),
                     F_WAX, 5)
    m = sk.affine(qe(roll=.35, pitch=.15), (ex + .15, ey - .75, ez))
    g.extend(hoop.transformed(m))
    for k in range(3):
        a = 2 * math.pi * k / 3 + .3
        hx, hy, hz = math.cos(a) * .55, 0.0, math.sin(a) * .55
        P = np.array(m).reshape(3, 4)
        w = P[:, :3] @ np.array([hx, hy, hz]) + P[:, 3]
        sk.sweep(g, sk.rect_section(.02, .01), [(ex, ey, ez), (float(w[0]), float(w[1]), float(w[2]))], F_IRON, up=(1, 0, 0))
    # the candelabrum: a tripod, a knopped stem, a dish with five arms and cups; toppled, lying along the floor
    cd = mk.Geo()
    sides = (8, 6, 4)[T.i]
    for k in range(3):
        a = 2 * math.pi * k / 3
        limb(cd, T, [(0, .3, 0), (math.cos(a) * .25, .12, math.sin(a) * .25), (math.cos(a) * .42, .0, math.sin(a) * .42)], .035, .025,
             F_IRON, sides=5, per=2)
    sk.lathe(cd, [(.05, .25), (.09, .32), (.05, .4), (.04, .9), (.08, .96), (.04, 1.02), (.035, 1.6), (.07, 1.66), (.035, 1.72),
                  (.04, 1.95), (.22, 2.0), (.26, 2.05)], (0, 0, 0), F_IRON, sides)
    for k in range(5):
        a = 2 * math.pi * k / 5
        tip = (math.cos(a) * .5, 2.2, math.sin(a) * .5)
        limb(cd, T, [(0, 1.98, 0), (math.cos(a) * .3, 2.0, math.sin(a) * .3), tip], .02, .018, F_IRON, sides=4, per=2)
        sk.lathe(cd, [(.0, 2.18), (.06, 2.2), (.055, 2.27)], (tip[0], 0, tip[2]), F_IRON, 5)
    g.extend(cd.transformed(sk.affine(qe(yaw=2.2, roll=-math.pi / 2 + .12), (-2.4, .36, -5.2))))
    for k, (x, z, yaw) in enumerate(((-3.2, -6.4, .4), (-1.4, -6.7, 1.9), (-3.7, -5.0, -.6))):
        sk.cylinder(g, (x, .03, z), (x + math.cos(yaw) * .2, .03, z + math.sin(yaw) * .2), .028, F_WAX, 5)


def outside(g, T):
    """Beyond the door: the ground outside in the dawn, a low broken wall and dark shapes of trees on the skyline."""
    sk.grid_face(g, (-16.0, -.06, ENT_FACE + 1.3), (32.0, 0, 0), (0, 0, 40.0), 0, 0, F_OUT, (0, 1, 0), (8.0, 16.0, 99.0)[T.i])
    sk.box(g, -6.0, -2.6, -.1, .9, 16.0, 16.6, F_STONE_DK, T, bevel=.04, cell=0)
    sk.box(g, 3.1, 7.4, -.1, .6, 17.5, 18.1, F_STONE_DK, T, bevel=.04, cell=0)
    r = mk.rng(121)
    for k in range((9, 7, 5)[T.i]):
        x = -18.0 + 36.0 * k / 8 + float(r.uniform(-1.5, 1.5))
        z = float(r.uniform(30, 44))
        h = float(r.uniform(7, 12))
        w = float(r.uniform(2.4, 3.6))
        prof = [(.4, 0.0), (.32, h * .3), (w * .75, h * .36), (w, h * .5), (w * .7, h * .6), (w * .95, h * .72), (w * .45, h * .88), (0.0, h)]
        sk.lathe(g, prof, (x, -.1, z), F_FAR, (6, 5, 4)[T.i], phase=float(r.uniform(0, 1)), scale=(1.0, float(r.uniform(.7, 1.0))))


def tufts(g, T, at, rad, n, seed, y=0.0):
    """Weeds and grass come up where a stone is gone or the rain gets in: blades and small leaves in a loose patch."""
    r = mk.rng(seed)
    x0, z0 = at
    for i in range(n):
        a = float(r.uniform(0, 2 * math.pi))
        d = rad * float(r.uniform(0, 1)) ** .7
        p = (x0 + math.cos(a) * d, y, z0 + math.sin(a) * d)
        b = float(r.uniform(0, 2 * math.pi))
        if i % 3 == 2:
            leaf_card(g, p, (math.cos(b), .35, math.sin(b)), float(r.uniform(.1, .16)), float(r.uniform(.06, .09)), F_IVY, F_IVY_DK,
                      fold=.2)
        else:
            leaf_card(g, p, (math.cos(b) * .3, 1.0, math.sin(b) * .3), float(r.uniform(.18, .32)), .035, F_GRASS, F_IVY_DK, fold=.1)
