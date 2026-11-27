"""The sleek pilot suit (owner 2026-11-27: "I think I want an improved design on the standard robot suit as well", then "Sleek pilot
suit": a fitted bodysuit with glowing seam lines under slimmer sculpted plates; docs/PILOT-BODY-HAIR.md section 5).

This part makes the bodysuit: two garments (the top: the torso, a mock-neck collar and the arms to the wrists; the legs: the
waist to the ankles) laid over the pilot body's own skin from its bind dump (the local tool's binddump with bindall=1 on Visual
body 1), as tools/gear/garments.py lays the zombies' clothes: the skin's region relaxed (Taubin) and held 4 mm off it, lined
round its openings, skinned with the nearest skin's four weights, and carrying the body's nine shapes (so it follows the female
body, and the plates over it follow it there: Runtime/NativeBodyKindPresentation.cs SuitFemaleEnsure). Its seams are raised
strips that take the pilot's accent colour and its glow: down each side, along each arm and leg, round the collar, the cuffs and
the hems, and a chevron across the chest. Written into the hair bank's file (tools/gear/hair.py) as body-skinned pieces.

usage (from hair.py): pilotsuit.build(dump_dir) -> list of piece dicts
"""
import math
import os
import sys

import numpy as np
from scipy.spatial import cKDTree

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import garments as G  # noqa: E402  (region, drape, lined, loops, compact: the zombies' garment toolkit)

SHAPES = G.SHAPES
EASE = .004
SEAM_W, SEAM_UP = .0055, .0018


class Body:
    """The pilot's skin from the dump (its parts welded), each vertex's weights and shape deltas, the bones' places; a KD-tree
    for the nearest skin (garments.Body's interface, without Blender)."""

    def __init__(self, dump):
        bind = G.read_bind(os.path.join(dump, 'bind.txt'))
        parts = sorted(r for r in bind if r.startswith('Body.Body_AnimeBasic'))
        V, F, W, S = [], [], [], {k: [] for k in SHAPES}
        for r in parts:
            v, f = G.read_obj(os.path.join(dump, 'rest.%s.obj' % r))
            names = [b for b, _ in bind[r]]
            w = G.read_w4(os.path.join(dump, 'rest.%s.w4' % r), names)
            sh = G.read_shapes(os.path.join(dump, 'rest.%s.shapes' % r), len(v))
            F.append(f + sum(len(x) for x in V))
            V.append(v)
            W += w
            for k in SHAPES:
                S[k].append(sh.get(k, np.zeros_like(v)))
        # (every part's bind poses: one skeleton, so a bone's is the same in each; a part may use bones another does not)
        self.bindposes = {}
        for r in parts:
            for b, m in bind[r]:
                self.bindposes.setdefault(b, m)
        self.P = {b: np.linalg.inv(m)[:3, 3] for b, m in self.bindposes.items()}
        V = np.concatenate(V)
        F = np.concatenate(F)
        key = np.round(V / 2e-4).astype(np.int64)
        _, first, remap = np.unique(key, axis=0, return_index=True, return_inverse=True)
        remap = remap.reshape(-1)
        self.V = V[first]
        self.W = [W[i] for i in first]
        self.S = {k: np.concatenate(S[k])[first] for k in SHAPES}
        F = remap[F]
        self.F = F[(F[:, 0] != F[:, 1]) & (F[:, 1] != F[:, 2]) & (F[:, 0] != F[:, 2])]
        self.N = G.normals(self.V, self.F)
        self.tree = cKDTree(self.V)

    def dominant(self, i):
        return max(self.W[i].items(), key=lambda x: x[1])[0] if self.W[i] else ''

    def near(self, p, k=4):
        d, j = self.tree.query(p, k)
        return [(self.V[jj], int(jj), float(dd)) for dd, jj in zip(np.atleast_1d(d), np.atleast_1d(j))]

    def bone(self, name):
        return self.P[name]


TORSO = ('spine_01', 'spine_02', 'spine_03', 'spine_04', 'spine_05', 'clavicle_l', 'clavicle_r', 'breast_l', 'breast_r', 'pelvis')
ARMS = ('upperarm_l', 'upperarm_r', 'upperarm_twist_01_l', 'upperarm_twist_01_r', 'upperarm_twist_02_l', 'upperarm_twist_02_r',
        'lowerarm_l', 'lowerarm_r', 'lowerarm_twist_01_l', 'lowerarm_twist_01_r', 'lowerarm_twist_02_l', 'lowerarm_twist_02_r',
        'shoulder_twist_l', 'shoulder_twist_r')
LEGS = ('pelvis', 'thigh_l', 'thigh_r', 'thigh_twist_01_l', 'thigh_twist_01_r', 'thigh_twist_01_l1', 'thigh_twist_01_r1',
        'calf_l', 'calf_r', 'calf_twist_01_l', 'calf_twist_01_r', 'calf_twist_01_l1', 'calf_twist_01_r1', 'spine_01')


def weight_on(body, i, bones):
    return sum(w for b, w in body.W[i].items() if b in bones)


def suit_top(body):
    """The torso from below the waist to a mock-neck collar, the arms to the wrists."""
    neck0 = body.bone('neck_01')
    head = body.bone('head')
    collar_y = neck0[1] + .45 * (head[1] - neck0[1])
    low_y = G.hips_y(body) - .03
    wrist = {s: body.bone('hand_' + s) for s in 'lr'}

    def keep(i, p):
        if p[1] < low_y or p[1] > collar_y:
            return False
        tw = weight_on(body, i, TORSO + ARMS + ('neck_01', 'neck_02'))
        if tw < .5:
            return False
        s = 'l' if p[0] < 0 else 'r'
        # (Unity's +x is the pilot's left: the l bones sit at -x in bind space)
        if abs(p[0]) > .2:
            hand = wrist[s]
            elbow = body.bone('lowerarm_' + s)
            axis = hand - elbow
            t = np.dot(p - elbow, axis) / np.dot(axis, axis)
            if t > .97:
                return False
        return True
    return G.region_i(body, keep)


def suit_legs(body):
    """The waist to the ankles."""
    waist = G.hips_y(body) + .045
    ankle_y = body.bone('foot_l')[1] + .075

    def keep(i, p):
        if p[1] > waist or p[1] < ankle_y:
            return False
        return weight_on(body, i, LEGS) > .5
    return G.region_i(body, keep)


def raise_strip(path_, width=SEAM_W, up=SEAM_UP):
    """A raised seam along a path laid on the suit (its points and the surface's normals there): a flat ribbon `width` wide
    lifted `up` off it, its two long edges sloped down to the surface."""
    pts, nrm = path_
    V, F = [], []
    n = len(pts)
    for i, p in enumerate(pts):
        t = pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]
        t = t / max(np.linalg.norm(t), 1e-9)
        side = np.cross(nrm[i], t)
        side = side / max(np.linalg.norm(side), 1e-9)
        V += [p - side * width * .9, p - side * width * .5 + nrm[i] * up, p + side * width * .5 + nrm[i] * up, p + side * width * .9]
    for i in range(n - 1):
        a, b = i * 4, (i + 1) * 4
        for k in range(3):
            F += [(a + k, b + k, a + k + 1), (a + k + 1, b + k, b + k + 1)]
    # each face wound outward (the pack's winding: cross(b - a, c - a) along the surface's normal)
    V = np.array(V)
    N = np.repeat(np.asarray(nrm), 4, axis=0)
    F = [(a, b, c) if np.dot(np.cross(V[b] - V[a], V[c] - V[a]), N[a] + N[b] + N[c]) >= 0 else (a, c, b) for a, b, c in F]
    return V, np.array(F, np.int64)


class Surface:
    """A suit shell to lay seams on: its vertices and normals, a KD-tree; a point projected onto the tangent plane of its nearest
    vertices (a smooth surface, not the vertices' steps)."""

    def __init__(self, V, F):
        self.V, self.N = V, G.normals(V, F)
        self.tree = cKDTree(V)

    def project(self, p, lift=0.0):
        d, j = self.tree.query(p, 6)
        w = 1 / np.maximum(d, 1e-5) ** 2
        w /= w.sum()
        q = (self.V[j] * w[:, None]).sum(0)
        n = (self.N[j] * w[:, None]).sum(0)
        n /= max(np.linalg.norm(n), 1e-9)
        return p - n * np.dot(p - q, n) + n * lift, n


def smooth_path(surf, pts, rounds=6, lift=.0012):
    pts = np.array([surf.project(p)[0] for p in pts])
    for _ in range(rounds):
        q = pts.copy()
        q[1:-1] = (pts[:-2] + 2 * pts[1:-1] + pts[2:]) / 4
        pts = np.array([surf.project(p)[0] for p in q])
    return np.array([surf.project(p, lift)[0] for p in pts]), np.array([surf.project(p)[1] for p in pts])


def path(surf, a, b, n=40):
    return smooth_path(surf, [a + (b - a) * t for t in np.linspace(0, 1, n)])


def ring(surf, centre, axis, radius_hint, n=48):
    """A loop round a limb or the neck: points on the suit around `centre` square to `axis`."""
    axis = axis / np.linalg.norm(axis)
    ref = np.array([0, 0, 1.]) if abs(axis[2]) < .9 else np.array([1., 0, 0])
    u = np.cross(axis, ref)
    u /= np.linalg.norm(u)
    v = np.cross(axis, u)
    pts = []
    for a in np.linspace(0, 2 * math.pi, n + 1):
        ray = u * math.cos(a) + v * math.sin(a)
        # the suit's farthest point out along the ray, near the ring's plane
        cand = [centre + ray * r for r in np.linspace(radius_hint * .3, radius_hint * 3, 24)]
        best = max(cand, key=lambda c: -abs(surf.tree.query(c)[0]) if True else 0)
        pts.append(best)
    return smooth_path(surf, pts, rounds=3)


def join(*paths):
    P = np.concatenate([p[0] if i == 0 else p[0][1:] for i, p in enumerate(paths)])
    N = np.concatenate([p[1] if i == 0 else p[1][1:] for i, p in enumerate(paths)])
    return P, N


def seams(body, ts, ls):
    """The glowing seams: paths over the suit top (`ts`) and legs (`ls`) surfaces."""
    B = body.bone
    top, legs = [], []
    hips = G.hips_y(body)
    for s in 'lr':
        sh, el, wr = B('upperarm_' + s), B('lowerarm_' + s), B('hand_' + s)
        sx = np.sign(sh[0])
        # down the side of the torso, from under the arm to the hip
        top.append(path(ts, np.array([sh[0] * .8, sh[1] - .085, sh[2] - .01]), np.array([B('thigh_' + s)[0] * 1.5, hips - .02, B('thigh_' + s)[2] - .015]), 40))
        # along the top of the arm, shoulder to wrist
        top.append(join(path(ts, sh + np.array([0, .05, -.01]), el + np.array([0, .04, -.012]), 28), path(ts, el + np.array([0, .04, -.012]), wr + np.array([0, .03, -.006]), 28)))
        # the cuff
        top.append(ring(ts, wr + (el - wr) * .07, wr - el, .025))
        # down the outside of the leg, hip to ankle
        th, ca, ft = B('thigh_' + s), B('calf_' + s), B('foot_' + s)
        o = np.array([sx * .09, 0, 0])
        legs.append(join(path(ls, th + o + np.array([0, .02, 0]), ca + o * .75, 34), path(ls, ca + o * .75, ft + o * .5 + np.array([0, .1, 0]), 34)))
        legs.append(ring(ls, ft + np.array([0, .092, 0]), ca - ft, .03))
        # the chest's chevron: from the front of the shoulder down to the sternum
        top.append(path(ts, np.array([sh[0] * .72, sh[1] + .0, .09]), np.array([0, B('spine_05')[1] - .06, .14]), 30))
    neck = B('neck_01')
    top.append(ring(ts, neck + np.array([0, .45 * (B('head')[1] - neck[1]) - .012, .005]), B('head') - neck, .045))
    top.append(ring(ts, np.array([0, hips - .02, .01]), np.array([0, 1., 0]), .09, n=72))
    legs.append(ring(ls, np.array([0, hips + .035, .01]), np.array([0, 1., 0]), .09, n=72))
    return top, legs


def garment(body, V, F, strips, name):
    """The suit piece as a hair-bank piece record (hair.py writes it): the draped shell (its texel the fabric's) with its lining
    and its seams (the accent's texel), skinned, the shapes carried."""
    V, F = G.lined(V, F, inset=.003)
    uv = np.tile([.3, .5], (len(V), 1))
    for pth in strips:
        sv, sf = raise_strip(pth)
        F = np.concatenate([F, sf + len(V)])
        V = np.concatenate([V, sv])
        uv = np.concatenate([uv, np.tile([.88, .5], (len(sv), 1))])
    g = G.Garment(name, 1)
    g.V, g.F = V, F
    g.skirted = False
    W, S = G.skin(body, g)
    return V, F, uv, W, S


def fabric_png():
    """The suit's colour weights: the fabric (its first colour, a little of its second) on the left, the accent on the right."""
    import io
    from PIL import Image
    img = np.zeros((16, 64, 4), np.uint8)
    img[:, :44] = (214, 41, 0, 255)
    img[:, 44:52] = (128, 64, 63, 255)
    img[:, 52:] = (0, 0, 255, 255)
    buf = io.BytesIO()
    Image.fromarray(img, 'RGBA').save(buf, 'PNG')
    return buf.getvalue()


def build(dump):
    body = Body(dump)
    tV, tF = suit_top(body)
    lV, lF = suit_legs(body)
    tV = G.drape(body, tV, tF, lambda p: EASE, rounds=20)
    lV = G.drape(body, lV, lF, lambda p: EASE, rounds=20)
    surfaces = Surface(tV, tF), Surface(lV, lF)
    st, sl = seams(body, *surfaces)
    out = []
    for name, (V, F, strips) in (('Top.Bank_PilotSuit', (tV, tF, st)), ('Bottom.Bank_PilotSuitLegs', (lV, lF, sl))):
        V, F, uv, W, S = garment(body, V, F, strips, name)
        out.append(dict(renderer=name, V=V, F=F, uv=uv, W=W, S=S))
    build.surfaces = surfaces  # (the plates are laid on these: tools/gear/suitplates.py)
    return body, out
