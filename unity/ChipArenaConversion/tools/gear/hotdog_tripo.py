"""The Hot Dog from the owner's Tripo model (owner 2026-11-08: "Implement this model as a dev skin (Do clean up on it if
necessary)", "Id like to also give the body physics (Its a hotdog lol)"): the owner's rigged FBX export retargeted onto the model's
own skeleton and written as the local bank Resources/ChipArenaHotDog.bytes (Runtime/NativeHotDogPresentation.cs). The FBX and the
bank stay local (the bank ships in the Drive zip, as the licensed packs do); this script only reads the file it is given.

The export: one mesh in 36 closed parts (the sausage with its face and mustard in relief, the pupils, the mouth and its teeth,
the crown's sprigs and the end knot, two arm tubes, two gloves, two leg tubes and the shoes' pieces), skinned to Tripo's
67-bone humanoid at rest with the arms hanging (the crossed arms it shows are its stored animations, unused here). It faces -Y,
its Left_ bones at +x, one unit tall. The game's bind space (Unity's): y up from the soles, z ahead, the pilot's right at +x,
so a point there is (-x, z, -y) of Blender's.

The retarget, each part by what it is:
- the sausage and everything on it (face, mouth, pupils, sprigs, knot) one rigid, uniformly scaled piece (SAUSAGE_SCALE) set
  on the pilot's spine: its bottom just under the hip joints (SAUSAGE_BOTTOM), centred on the spine where the shoulders are,
  and its weights the pilot's spine by height (a bell round each spine bone, so a bent spine bends it smoothly), on the
  spine's twins (bone names "~pelvis" ..: the runtime makes each a child of that bone, where the sausage's jiggle goes, so the
  arms, the hands and whatever they hold never move with it);
- the limbs (arm and leg tubes, gloves, shoes) bone by bone: each Tripo bone's frame onto the pilot's matching bone, stretched
  along the bone to the pilot's length (the noodle limbs grow to the pilot's reach and stride) and scaled across by
  LIMB_THICK; the hands, fingers and feet not stretched, only scaled (HAND_SCALE, SHOE_SCALE), each finger laid along the
  pilot's finger so the pack's grips close the glove. Weights carried over by name (Left_ is the pilot's left, "_l");
- the shoes' soles set on the floor.

Colour: the export's texture when it is given (--texture, embedded as it is), else one painted here from the parts and the
sausage's relief (a hot dog red, the mustard yellow, white eyes with black pupils, dark brows, white teeth in a dark mouth,
black noodle limbs, white gloves, red shoes with white soles, toe caps, laces and cuffs). The texture sits in the bank.

The bank (HDOG v2): the bone names its weights use (a twin's "~" + its bone), then per vertex its place, normal, RGBA colour (sRGB
bytes), texture coordinate, four bone indices and four weights, the triangles (cross(b - a, c - a) outward, as the body's own),
then the texture's bytes (PNG or JPEG; length 0 without one).

usage: blender --background --factory-startup --python tools/gear/hotdog_tripo.py -- --fbx FILE --dump DIR
           --out ASSETS/LocalAssets [--texture IMAGE] [--preview DIR]
       --dump: a bind dump of the model (the local tool's binddump; bind.txt and rest.Body.Body_AnimeBasic*.obj)
"""
import argparse
import math
import os
import struct
import sys
import zlib

import numpy as np

BANK = 'ChipArenaHotDog'
RESOURCES = 'ChipArenaLocal/Resources'

SAUSAGE_SCALE = 1.3     # the sausage's size against the export's
SAUSAGE_BOTTOM = 0.92   # where its lowest point sits (m; the hip joints at 0.961 inside it)
LIMB_THICK = 1.3        # the noodle limbs' girth against the export's
HAND_SCALE = 1.3        # the gloves'
SHOE_SCALE = 1.2        # the shoes' length and width (their height kept so the ankle is the pilot's)
TEXTURE = 1024          # the painted texture's size
SPINE_SPREAD = .12      # how far each spine bone's hold on the sausage reaches (m, the bell's width)

# The pilot's spine the sausage rides, bottom to top (its twins carry the weights).
SPINE = ['pelvis', 'spine_02', 'spine_03', 'spine_04', 'spine_05', 'neck_01', 'head']
TRIPO_SPINE = {'Hips', 'Spine', 'Chest', 'UpperChest', 'Neck', 'Neck_Twist_A', 'Head', 'Left_Eye', 'Right_Eye'}
FINGERS = [('Thumb', 'thumb'), ('Index', 'index'), ('Middle', 'middle'), ('Ring', 'ring'), ('Pinky', 'pinky')]
JOINTS = [('Proximal', '01'), ('Intermediate', '02'), ('Distal', '03')]


def limb_map():
    """Tripo bone -> (pilot bone, Tripo child joint, pilot child joint or None, kind)."""
    out = {}
    for side, s in (('Left', 'l'), ('Right', 'r')):
        T = lambda n: '%s_%s' % (side, n)  # noqa: E731
        P = lambda n: '%s_%s' % (n, s)  # noqa: E731
        out[T('Shoulder')] = (P('clavicle'), T('UpperArm'), P('upperarm'), 'limb')
        out[T('UpperArm')] = (P('upperarm'), T('LowerArm'), P('lowerarm'), 'limb')
        out[T('LowerArm')] = (P('lowerarm'), T('Hand'), P('hand'), 'limb')
        out[T('Hand')] = (P('hand'), T('MiddleProximal'), P('middle_01'), 'hand')
        for tf, pf in FINGERS:
            for k, (tj, pj) in enumerate(JOINTS):
                nxt = JOINTS[k + 1][0] if k + 1 < len(JOINTS) else 'DistalEnd'
                pnext = '%s_%s' % (pf, JOINTS[k + 1][1]) if k + 1 < len(JOINTS) else None
                out[T(tf + tj)] = (P('%s_%s' % (pf, pj)), T(tf + nxt), P(pnext) if pnext else None, 'finger')
        out[T('UpperLeg')] = (P('thigh'), T('LowerLeg'), P('calf'), 'limb')
        out[T('LowerLeg')] = (P('calf'), T('Foot'), P('foot'), 'limb')
        out[T('Foot')] = (P('foot'), T('Toes'), P('ball'), 'foot')
        out[T('Toes')] = (P('ball'), T('ToesEnd'), None, 'foot')
    return out


def to_game(p):
    p = np.asarray(p, dtype=np.float64)
    return np.stack([-p[..., 0], p[..., 2], -p[..., 1]], axis=-1)


def unit(v):
    v = np.asarray(v, dtype=np.float64)
    return v / (np.linalg.norm(v, axis=-1, keepdims=True) + 1e-12)


# ---------------------------------------------------------------------------------------------------------------------------
# Reading
# ---------------------------------------------------------------------------------------------------------------------------
def read_fbx(path):
    """The export at rest: vertices, triangles, each triangle's corners' texture coordinates, weights, bone heads (Blender's
    space), and the texture file it names."""
    import bpy
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
    ob = [o for o in bpy.data.objects if o.type == 'MESH'][0]
    me = ob.data
    V = np.array([(ob.matrix_world @ v.co)[:] for v in me.vertices])
    me.calc_loop_triangles()
    F = np.array([t.vertices[:] for t in me.loop_triangles], dtype=np.int64)
    uvl = me.uv_layers[0].data
    UV = np.array([[uvl[l].uv[:] for l in t.loops] for t in me.loop_triangles])
    names = [g.name for g in ob.vertex_groups]
    W = np.zeros((len(V), len(names)))
    for v in me.vertices:
        for g in v.groups:
            W[v.index, g.group] = g.weight
    heads = {b.name: np.array((arm.matrix_world @ b.head_local)[:]) for b in arm.data.bones}
    named = [i.filepath for i in bpy.data.images]
    return V, F, UV, W, names, heads, named


def read_dump(dump):
    """The pilot's bones at bind (their places, from Body.Body_AnimeBasic's bind poses) and its body's surface (preview)."""
    P = {}
    with open(os.path.join(dump, 'bind.txt')) as fh:
        for line in fh:
            a = line.split()
            if len(a) != 18 or a[1] == 'transform' or a[0] != 'Body.Body_AnimeBasic':
                continue
            P[a[1]] = np.linalg.inv(np.array([float(x) for x in a[2:]]).reshape(4, 4))[:3, 3]
    body = []
    for f in sorted(os.listdir(dump)):
        if f.startswith('rest.Body.Body_AnimeBasic') and f.endswith('.obj'):
            vs, fs = [], []
            with open(os.path.join(dump, f)) as fh:
                for line in fh:
                    if line.startswith('v '):
                        vs.append([float(x) for x in line.split()[1:4]])
                    elif line.startswith('f '):
                        fs.append([int(x.split('/')[0]) - 1 for x in line.split()[1:4]])
            body.append((np.array(vs), np.array(fs)))
    return P, body


def components(n, F):
    parent = np.arange(n)

    def find(a):
        r = a
        while parent[r] != r:
            r = parent[r]
        while parent[a] != r:
            parent[a], a = r, parent[a]
        return r
    for f in F:
        a = find(f[0])
        for b in f[1:]:
            rb = find(b)
            if rb != a:
                parent[rb] = a
    roots = np.array([find(i) for i in range(n)])
    _, comp, counts = np.unique(roots, return_inverse=True, return_counts=True)
    order = np.argsort(-counts, kind='stable')
    rank = np.empty_like(order)
    rank[order] = np.arange(len(order))
    return rank[comp.reshape(-1)]


def neighbours(n, F):
    nb = [set() for _ in range(n)]
    for a, b, c in F:
        nb[a].update((b, c))
        nb[b].update((a, c))
        nb[c].update((a, b))
    return [np.array(sorted(x), dtype=np.int64) for x in nb]


def vertex_normals(V, F):
    N = np.zeros_like(V)
    fn = np.cross(V[F[:, 1]] - V[F[:, 0]], V[F[:, 2]] - V[F[:, 0]])
    for k in range(3):
        np.add.at(N, F[:, k], fn)
    return unit(N)


# ---------------------------------------------------------------------------------------------------------------------------
# The parts
# ---------------------------------------------------------------------------------------------------------------------------
class Parts:
    """Each vertex's part: what it is (sausage, arm, hand, leg, shoe) and the finer kind the paint uses."""

    def __init__(self, V, F, W, names):
        n = len(V)
        self.rank = components(n, F)
        dom = np.array([names[j] for j in W.argmax(1)])
        self.kind = np.empty(n, dtype=object)
        self.paint = np.empty(n, dtype=object)
        for r in range(self.rank.max() + 1):
            idx = np.where(self.rank == r)[0]
            bones = {}
            for j in W[idx].argmax(1):
                bones[names[j]] = bones.get(names[j], 0) + 1
            top = max(bones, key=bones.get)
            lo, hi = V[idx].min(0), V[idx].max(0)
            size = hi - lo
            if top in TRIPO_SPINE:
                kind = 'sausage'
                if r == 0:
                    paint = 'sausage'
                elif lo[2] > .9:
                    paint = 'sprig'
                elif hi[2] < .35:
                    paint = 'knot'
                elif len(idx) < 40:
                    paint = 'tooth'
                elif size[0] > .1:
                    paint = 'mouth'
                else:
                    paint = 'pupil'
            elif 'Arm' in top or 'Shoulder' in top:
                kind, paint = 'arm', 'limb'
            elif 'Leg' in top:
                kind, paint = 'leg', 'limb'
            elif 'Foot' in top or 'Toes' in top:
                kind = 'shoe'
                if hi[2] < .03:
                    paint = 'sole'
                elif hi[2] < .04:
                    paint = 'midsole'
                elif lo[2] > .1:
                    paint = 'cuff'
                elif len(idx) < 150:
                    paint = 'lace'
                elif lo[1] < -.14 and hi[1] < -.08:
                    paint = 'toecap'
                else:
                    paint = 'upper'
            else:
                kind, paint = 'hand', 'glove'
            self.kind[idx] = kind
            self.paint[idx] = paint


# ---------------------------------------------------------------------------------------------------------------------------
# The retarget
# ---------------------------------------------------------------------------------------------------------------------------
def frame(a, ref):
    """Columns: the bone's axis, then two across it (the second toward `ref`)."""
    x = unit(a)
    z = unit(ref - np.dot(ref, x) * x)
    y = np.cross(z, x)
    return np.stack([x, y, z], axis=1)


def palm(J, hand, index, middle, pinky):
    """A hand's palm normal-ish (across the knuckles, crossed with the hand's length), the same way round on both skeletons."""
    across = J[index] - J[pinky]
    along = J[middle] - J[hand]
    return unit(np.cross(along, across))


class Retarget:
    def __init__(self, V, F, W, names, heads, P):
        self.V, self.F, self.W, self.names, self.P = V, F, W, names, P
        self.H = {k: to_game(v) for k, v in heads.items()}
        self.parts = Parts(V, F, W, names)
        self.U = to_game(V)
        self.map = limb_map()
        sausage = self.parts.kind == 'sausage'
        S = self.U[sausage]
        s = SAUSAGE_SCALE
        hips = self.H['Hips']
        # Its height: the bottom on SAUSAGE_BOTTOM. Across: its centre where the pilot's shoulders are, on the spine.
        y0 = SAUSAGE_BOTTOM + s * (hips[1] - S[:, 1].min())
        shoulder_t = hips[1] + (P['upperarm_l'][1] - y0) / s
        band = S[np.abs(S[:, 1] - shoulder_t) < .02]
        centre = (band.min(0) + band.max(0)) / 2
        shoulder_z = (P['upperarm_l'][2] + P['upperarm_r'][2]) / 2
        self.s_anchor = np.array([0.0, y0, shoulder_z - s * (centre[2] - hips[2])])
        self.s_from = hips
        self.shoulder_t = shoulder_t
        for tb in list(heads):
            if tb.endswith('End') and tb[:-3] in self.map:
                self.map[tb] = self.map[tb[:-3]]  # (an end bone's weights go to its joint)
        self.xf = {}
        for tb in names:
            if tb.endswith('End') and tb in self.map:
                self.xf[tb] = ('limb', self.limb_xf(tb[:-3]))
            elif tb in TRIPO_SPINE:
                self.xf[tb] = ('sausage', None)
            elif tb in self.map:
                self.xf[tb] = ('limb', self.limb_xf(tb))
            else:
                self.xf[tb] = ('none', None)

    def sausage_xf(self, U):
        return self.s_anchor + SAUSAGE_SCALE * (U - self.s_from)

    def limb_xf(self, tb):
        pb, tchild, pchild, kind = self.map[tb]
        H, P = self.H, self.P
        side = tb.split('_')[0]
        s = 'l' if side == 'Left' else 'r'
        h0, c0 = H[tb], H[tchild]
        p0 = P[pb]
        if kind == 'foot':
            # The shoe one rigid piece on the foot (its toe part too, weighted to the ball): longer and wider, its height kept,
            # so the export's ankle over its sole is the pilot's over the floor.
            return H['%s_Foot' % side], P['foot_' + s], np.diag([SHOE_SCALE, 1.0, SHOE_SCALE])
        if pchild is None:
            # (A finger's last joint: along its parent's line, as long as the export's scaled.)
            d = unit(P[pb] - P[pb.replace('_03_', '_02_')])
            p1 = p0 + d * np.linalg.norm(c0 - h0) * HAND_SCALE
        else:
            p1 = P[pchild]
        a0, a1 = c0 - h0, p1 - p0
        L0, L1 = np.linalg.norm(a0), np.linalg.norm(a1)
        if kind in ('hand', 'finger'):
            hand_t = '%s_Hand' % side
            ref0 = palm(H, hand_t, side + '_IndexProximal', side + '_MiddleProximal', side + '_PinkyProximal')
            ref1 = palm(P, 'hand_' + s, 'index_01_' + s, 'middle_01_' + s, 'pinky_01_' + s)
            along, across = HAND_SCALE, HAND_SCALE
        else:
            ref0 = ref1 = np.array([0.0, 0.0, 1.0])
            along, across = L1 / max(L0, 1e-6), LIMB_THICK
        F0, F1 = frame(a0, ref0), frame(a1, ref1)
        M = F1 @ np.diag([along, across, across]) @ F0.T
        return h0, p0, M

    def apply_limb(self, tb, U):
        h0, p0, M = self.xf[tb][1]
        return p0 + (U - h0) @ M.T

    def run(self):
        U, W, names = self.U, self.W, self.names
        n = len(U)
        out = np.zeros_like(U)
        sausage = self.parts.kind == 'sausage'
        SU = self.sausage_xf(U)
        out[sausage] = SU[sausage]
        for j, tb in enumerate(names):
            w = W[:, j] * ~sausage
            if not w.any():
                continue
            kind, _ = self.xf[tb]
            if kind == 'limb':
                out += w[:, None] * self.apply_limb(tb, U)
            else:
                out += w[:, None] * SU
        tot = W.sum(1)
        out[~sausage] /= tot[~sausage, None]
        # The shoes' soles on the floor: each shoe's lowest point to 0 by moving its foot part down (the cuff keeps the leg).
        self.U2 = out
        return out

    def weights(self, X):
        """Each vertex's weights on the pilot's bones: the sausage's by height on the spine's twins, the limbs' carried over
        (and their spine share on the twins by height too)."""
        W, names = self.W, self.names
        n = len(X)
        spine_y = np.array([self.P[b][1] for b in SPINE])
        # Each height's share of the spine's bones: a bell round each bone (SPINE_SPREAD wide), heights past the ends held at
        # them, so a bend of the pilot's spine spreads down the sausage instead of folding it at one joint.
        y = np.clip(X[:, 1], spine_y[0], spine_y[-1])
        bell = np.exp(-.5 * ((y[:, None] - spine_y[None, :]) / SPINE_SPREAD) ** 2)
        rows = []
        sausage = self.parts.kind == 'sausage'
        for i in range(n):
            row = {}
            if sausage[i]:
                share = 1.0
            else:
                share = 0.0
                tot = W[i].sum()
                for j in np.nonzero(W[i])[0]:
                    tb = names[j]
                    if self.xf[tb][0] == 'limb':
                        pb = self.map[tb][0]
                        row[pb] = row.get(pb, 0) + W[i, j] / tot
                    else:
                        share += W[i, j] / tot
            if share > 0:
                top = np.argsort(-bell[i])[:4]
                ws = bell[i, top] / bell[i, top].sum()
                for k, w in zip(top, ws):
                    if w * share > 1e-4:
                        row['~' + SPINE[k]] = row.get('~' + SPINE[k], 0) + w * share
            top = sorted(row.items(), key=lambda x: -x[1])[:4]
            tot = sum(w for _, w in top)
            rows.append([(b, w / tot) for b, w in top])
        return rows


# ---------------------------------------------------------------------------------------------------------------------------
# The painted texture (without the export's own)
# ---------------------------------------------------------------------------------------------------------------------------
def srgb(h):
    return np.array([(h >> 16 & 255) / 255.0, (h >> 8 & 255) / 255.0, (h & 255) / 255.0])


COL = {
    'sausage': srgb(0xC8452F), 'sausage_end': srgb(0x9E3324), 'sprig': srgb(0xA6382A), 'knot': srgb(0xA6382A),
    'mustard': srgb(0xF4C316), 'brow': srgb(0x2A1714), 'eye': srgb(0xF7F4EC), 'eye_line': srgb(0x2A1714),
    'pupil': srgb(0x16110F), 'mouth': srgb(0x3A1412), 'tooth': srgb(0xF7F4EC),
    'limb': srgb(0x1F1E22), 'glove': srgb(0xF4F3EF), 'upper': srgb(0xD8322C), 'toecap': srgb(0xF4F3EF),
    'sole': srgb(0xF4F3EF), 'midsole': srgb(0xB9271F), 'lace': srgb(0xF4F3EF), 'cuff': srgb(0xF4F3EF),
}


def relief(V, F, sausage):
    """How far each of the sausage's vertices stands proud of its smoothed surface (Taubin, so it doesn't shrink)."""
    nb = neighbours(len(V), F)
    S = V.copy()

    def lap(X):
        return np.array([X[x].mean(0) if len(x) else X[i] for i, x in enumerate(nb)]) - X
    for _ in range(40):
        S = S + .6 * lap(S)
        S = S - .63 * lap(S)
    N = vertex_normals(V, F)
    r = ((V - S) * N).sum(1)
    r[~sausage] = 0
    return r


def eyes(V, parts):
    """The eye whites round each pupil: an upright ellipse on the face (Blender's x, z), centred out from the pupil."""
    out = []
    for r in range(parts.rank.max() + 1):
        idx = np.where(parts.rank == r)[0]
        if parts.paint[idx[0]] == 'pupil':
            c = V[idx].mean(0)
            out.append((np.sign(c[0]) * .0405, .805, .031, .051))
    return out


def paint_colour(P, Nrm, R, paint, eye_list):
    """The colour at points P (Blender's space) of a part painted `paint` (relief R: the sausage's)."""
    n = len(P)
    if paint != 'sausage':
        return np.repeat(COL[paint][None], n, axis=0)
    c = np.repeat(COL['sausage'][None], n, axis=0)
    z = P[:, 2]
    # Browned toward its rounded ends.
    end = np.clip(np.maximum((z - .86) / .1, (.36 - z) / .08), 0, 1)[:, None]
    c = c * (1 - end * .55) + COL['sausage_end'] * end * .55
    front = (Nrm[:, 1] < .1) & (P[:, 1] < 0)
    for ex, ez, ax, az in eye_list:
        d = ((P[:, 0] - ex) / ax) ** 2 + ((z - ez) / az) ** 2
        c[front & (d < 1.0)] = COL['eye_line']
        c[front & (d < .78)] = COL['eye']
    # The brows: the relief along the two bars slanting down to meet over the nose (the angry look's).
    ax = np.abs(P[:, 0])
    brow = front & (R > .001) & (np.abs(z - (.840 + .55 * ax)) < .020) & (ax < .085)
    c[brow] = COL['brow']
    mustard = front & (R > .0018) & (z < .66) & (z > .33)
    c[mustard] = COL['mustard']
    return c


def paint(V, F, UVt, parts, size=TEXTURE):
    """Rasterise each triangle into the texture by its corners' texture coordinates; dilate the islands' edges."""
    sausage = parts.paint == 'sausage'
    R = relief(V, F, sausage)
    N = vertex_normals(V, F)
    eye_list = eyes(V, parts)
    img = np.zeros((size, size, 3))
    filled = np.zeros((size, size), dtype=bool)
    for t in range(len(F)):
        a, b, c = F[t]
        uv = UVt[t] * size - .5
        lo = np.floor(uv.min(0)).astype(int)
        hi = np.ceil(uv.max(0)).astype(int)
        lo = np.clip(lo - 1, 0, size - 1)
        hi = np.clip(hi + 1, 0, size - 1)
        xs, ys = np.meshgrid(np.arange(lo[0], hi[0] + 1), np.arange(lo[1], hi[1] + 1))
        p = np.stack([xs.ravel(), ys.ravel()], 1).astype(np.float64)
        v0, v1 = uv[1] - uv[0], uv[2] - uv[0]
        den = v0[0] * v1[1] - v1[0] * v0[1]
        if abs(den) < 1e-12:
            continue
        d = p - uv[0]
        l1 = (d[:, 0] * v1[1] - v1[0] * d[:, 1]) / den
        l2 = (v0[0] * d[:, 1] - d[:, 0] * v0[1]) / den
        l0 = 1 - l1 - l2
        eps = .6 / max(np.linalg.norm(v0), np.linalg.norm(v1), 1.0)
        inside = (l0 > -eps) & (l1 > -eps) & (l2 > -eps)
        if not inside.any():
            continue
        L = np.stack([l0, l1, l2], 1)[inside]
        P = L @ V[[a, b, c]]
        Nr = unit(L @ N[[a, b, c]])
        Rr = L @ R[[a, b, c]]
        col = paint_colour(P, Nr, Rr, parts.paint[a], eye_list)
        px, py = p[inside, 0].astype(int), p[inside, 1].astype(int)
        img[size - 1 - py, px] = col
        filled[size - 1 - py, px] = True
    for _ in range(6):
        grow = ~filled
        acc = np.zeros_like(img)
        cnt = np.zeros(filled.shape)
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            sh = np.roll(np.roll(filled, dy, 0), dx, 1)
            acc += np.roll(np.roll(img, dy, 0), dx, 1) * sh[..., None]
            cnt += sh
        take = grow & (cnt > 0)
        img[take] = acc[take] / cnt[take, None]
        filled |= take
    return png(np.clip(np.round(img * 255), 0, 255).astype(np.uint8))


def png(rgb):
    h, w, _ = rgb.shape
    raw = b''.join(b'\x00' + rgb[y].tobytes() for y in range(h))

    def chunk(t, d):
        return struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    return b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 2, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(raw, 9)) + chunk(b'IEND', b'')


# ---------------------------------------------------------------------------------------------------------------------------
# The bank
# ---------------------------------------------------------------------------------------------------------------------------
def split_uv(F, UVt):
    """One vertex per (vertex, texture coordinate) pair: the bank's vertices, their source vertices and their triangles."""
    key = {}
    src, uvs = [], []
    tris = np.zeros_like(F)
    for t in range(len(F)):
        for k in range(3):
            v = int(F[t, k])
            u = UVt[t, k]
            kk = (v, int(round(u[0] * 65536)), int(round(u[1] * 65536)))
            i = key.get(kk)
            if i is None:
                i = key[kk] = len(src)
                src.append(v)
                uvs.append(u)
            tris[t, k] = i
    return np.array(src), np.array(uvs), tris


def outward(X, F, rank):
    """Triangles wound so cross(b - a, c - a) points out of each closed part (the parts' signed volumes positive)."""
    F = F.copy()
    for r in range(rank.max() + 1):
        sel = rank[F[:, 0]] == r
        if not sel.any():
            continue
        T = F[sel]
        a, b, c = X[T[:, 0]], X[T[:, 1]], X[T[:, 2]]
        vol = np.einsum('ij,ij->i', a, np.cross(b, c)).sum()
        if vol < 0:
            F[sel] = T[:, [0, 2, 1]]
    return F


def write_bank(path, X, N, C, UV, rows, F, texture):
    names = sorted({b for row in rows for b, _ in row})
    of = {b: i for i, b in enumerate(names)}
    blob = bytearray(b'HDOG')
    blob += struct.pack('<ii', 2, len(names))
    for b in names:
        e = b.encode('utf-8')
        blob += struct.pack('<H', len(e)) + e
    n = len(X)
    blob += struct.pack('<i', n)
    blob += np.asarray(X, '<f4').tobytes() + np.asarray(N, '<f4').tobytes()
    blob += np.clip(np.round(C * 255), 0, 255).astype(np.uint8).tobytes()
    blob += np.asarray(UV, '<f4').tobytes()
    idx = np.zeros((n, 4), dtype='<u2')
    wts = np.zeros((n, 4), dtype='<f4')
    for i, row in enumerate(rows):
        for k, (b, w) in enumerate(row):
            idx[i, k] = of[b]
            wts[i, k] = w
    blob += idx.tobytes() + wts.tobytes()
    blob += struct.pack('<i', len(F)) + np.asarray(F, '<u4').tobytes()
    blob += struct.pack('<i', len(texture)) + texture
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, 'wb') as fh:
        fh.write(bytes(blob))
    meta = path + '.meta'
    if not os.path.exists(meta):
        import uuid
        with open(meta, 'w', newline='\n') as fh:
            fh.write('fileFormatVersion: 2\nguid: %s\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' % uuid.uuid4().hex)
    return len(blob), names


# ---------------------------------------------------------------------------------------------------------------------------
# A sheet: the skin beside the pilot's body, from the front, three-quarters, the side and behind, and close ups
# ---------------------------------------------------------------------------------------------------------------------------
def preview(X, F, UV, texture, body, out_dir):
    import bpy
    from mathutils import Vector
    os.makedirs(out_dir, exist_ok=True)
    tex_path = os.path.join(out_dir, 'hotdog_texture.' + ('png' if texture[:4] == b'\x89PNG' else 'jpg'))
    with open(tex_path, 'wb') as fh:
        fh.write(texture)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    img = bpy.data.images.load(tex_path)
    mat = bpy.data.materials.new('hd')
    mat.use_nodes = True
    nt = mat.node_tree
    tn = nt.nodes.new('ShaderNodeTexImage')
    tn.image = img
    nt.links.new(tn.outputs[0], nt.nodes['Principled BSDF'].inputs['Base Color'])
    ghost = bpy.data.materials.new('ghost')
    ghost.diffuse_color = (.55, .6, .7, 1)

    def place(name, V, Fs, yaw, dx, uv=None, m=mat):
        c, s = math.cos(math.radians(yaw)), math.sin(math.radians(yaw))
        x, y, z = -V[:, 0], -V[:, 2], V[:, 1]
        P = np.stack([x * c - y * s + dx, x * s + y * c, z], axis=1)
        me = bpy.data.meshes.new(name)
        # (Mirrored back into Blender's right-handed space, so each triangle's corners turn the other way.)
        me.from_pydata([tuple(p) for p in P], [], [(int(a), int(c0), int(b)) for a, b, c0 in Fs])
        me.update()
        if uv is not None:
            layer = me.uv_layers.new(name='uv')
            for poly in me.polygons:
                for li in poly.loop_indices:
                    layer.data[li].uv = uv[me.loops[li].vertex_index]
        for p in me.polygons:
            p.use_smooth = True
        me.materials.append(m)
        ob = bpy.data.objects.new(name, me)
        scene.collection.objects.link(ob)
        return ob

    scene.render.engine = 'BLENDER_WORKBENCH'
    sh = scene.display.shading
    sh.light = 'STUDIO'
    sh.color_type = 'TEXTURE'
    sh.show_object_outline = True
    sh.object_outline_color = (0, 0, 0)
    scene.world = bpy.data.worlds.new('w')
    scene.world.color = (.2, .22, .26)
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam'))
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam.data.type = 'ORTHO'
    for name, yaw, dx in (('front', 0, -2.4), ('tq', 38, -.8), ('side', 90, .8), ('back', 180, 2.4)):
        place(name, X, F, yaw, dx, UV)
    for k, (bv, bf) in enumerate(body):
        place('pilot%d' % k, bv, bf, 0, -3.4, None, ghost)
    scene.render.resolution_x, scene.render.resolution_y = 1800, 760
    cam.data.ortho_scale = 7.6
    cam.location = Vector((-.5, -6, 1.02))
    cam.rotation_euler = (math.radians(90), 0, 0)
    scene.render.filepath = os.path.join(out_dir, 'sheet.png')
    bpy.ops.render.render(write_still=True)
    for name, scale, at in (('face', .75, (-2.4, 1.55)), ('hand', .45, (-2.4 + .43, 1.0)), ('shoe', .6, (.8, .15))):
        scene.render.resolution_x, scene.render.resolution_y = 900, 760
        cam.data.ortho_scale = scale
        cam.location = Vector((at[0], -6, at[1]))
        scene.render.filepath = os.path.join(out_dir, name + '.png')
        bpy.ops.render.render(write_still=True)


def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else sys.argv[1:]
    ap = argparse.ArgumentParser()
    ap.add_argument('--fbx', required=True)
    ap.add_argument('--dump', required=True)
    ap.add_argument('--out', required=True, help='Assets/LocalAssets, or a scratch folder')
    ap.add_argument('--texture', help="the export's texture image (embedded as it is)")
    ap.add_argument('--preview')
    a = ap.parse_args(argv)
    V, F, UVt, W, names, heads, named = read_fbx(a.fbx)
    P, body = read_dump(a.dump)
    rt = Retarget(V, F, W, names, heads, P)
    X = rt.run()
    rows = rt.weights(X)
    if a.texture:
        with open(a.texture, 'rb') as fh:
            texture = fh.read()
    else:
        texture = paint(V, F, UVt, rt.parts)
    src, UV, T = split_uv(F, UVt)
    T = outward(X[src], T, rt.parts.rank[src])
    N = vertex_normals(X, outward(X, F, rt.parts.rank))[src]
    Xs = X[src]
    C = np.ones((len(src), 4))
    size, bnames = write_bank(os.path.join(a.out, RESOURCES, BANK + '.bytes'), Xs, N, C, UV, [rows[i] for i in src], T, texture)
    lo, hi = Xs.min(0), Xs.max(0)
    print('hot dog: %d vertices, %d triangles, %d bones, %.2f MB, texture %s %.2f MB' % (
        len(Xs), len(T), len(bnames), size / 1e6, 'given' if a.texture else 'painted', len(texture) / 1e6))
    print('  bounds', lo.round(3), hi.round(3), 'sausage anchor', rt.s_anchor.round(3), 'shoulder at export z %.3f' % rt.shoulder_t)
    print('  textures it names:', named)
    if a.preview:
        preview(Xs, T, UV, texture, body, a.preview)


if __name__ == '__main__':
    main()
