"""Chip Arena low-poly male base (Phase B): rebuilds male_base.blend from nothing.

Run headless with Blender 4.2 LTS (from the repository root):
    blender -b --factory-startup --python-exit-code 1 \
        --python unity/ChipArenaConversion/Art/Characters/male-base/build_male_base.py -- [--out path.blend]

Deterministic: fixed tables and a fixed-seed LCG only, no timestamps, factory settings. This script is the editable source;
male_base.blend is its output, and export_male_base.py writes the FBX, GLB, binary mesh and JSON manifest from that file.

Modelling space ("rig space") is the game's CapturedRig source frame: +x the character's left, +y up, +z forward, metres,
origin on the floor under the motion root. Blender space is rig space turned so the character faces Blender -Y:
blender(x, y, z) = (x, -z_rig, y_rig). Unity's frame is rig space with x mirrored (docs/CHARACTER-LOWPOLY.md, b and c).
The skeleton is exactly CapturedRig.Bones (22 joints, same names, order, parents and rest offsets, identity rest frames),
plus optional secondary hair chains (sec-hair-*) parented to `head`, which a runtime may ignore.
"""
import bisect
import json
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
ARGV = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []


def arg(name, default):
    return ARGV[ARGV.index(name) + 1] if name in ARGV else default


OUT_BLEND = arg('--out', os.path.join(HERE, 'male_base.blend'))

# ----------------------------------------------------------------------------------------------------------------- rig
# CapturedRig.Bones (Core/CapturedRig.cs): name, parent, local rest offset in the source frame. Identity rest rotations:
# every joint frame is the character frame at rest (arms hanging straight down, palms in, thumbs forward).
GAME_BONES = [
    ('motion-root', None, (0, 0, 0)), ('pelvis', 'motion-root', (0, -.24, 0)), ('spine', 'pelvis', (0, .105, 0)),
    ('chest', 'spine', (0, .19, 0)), ('neck', 'chest', (0, .32, 0)), ('head', 'neck', (0, .1, 0)),
    ('left-clavicle', 'chest', (.275, .18, 0)), ('left-shoulder', 'left-clavicle', (.07, 0, 0)),
    ('left-elbow', 'left-shoulder', (0, -.27, 0)), ('left-wrist', 'left-elbow', (0, -.25, 0)),
    ('right-clavicle', 'chest', (-.275, .18, 0)), ('right-shoulder', 'right-clavicle', (-.07, 0, 0)),
    ('right-elbow', 'right-shoulder', (0, -.27, 0)), ('right-wrist', 'right-elbow', (0, -.25, 0)),
    ('left-hip', 'pelvis', (.13, -.055, 0)), ('left-knee', 'left-hip', (0, -.25, 0)),
    ('left-ankle', 'left-knee', (0, -.25, 0)), ('left-toe', 'left-ankle', (0, -.02, .09)),
    ('right-hip', 'pelvis', (-.13, -.055, 0)), ('right-knee', 'right-hip', (0, -.25, 0)),
    ('right-ankle', 'right-knee', (0, -.25, 0)), ('right-toe', 'right-ankle', (0, -.02, .09)),
]
GAME_ROOT_HEIGHT = .865  # the game places motion-root at feet + .865 (NativeMeleeSlice.RootHeight)
GAME_OFFSET = {n: o for n, _, o in GAME_BONES}
PARENT = {n: p for n, p, _ in GAME_BONES}
BONE_NAMES = [n for n, _, _ in GAME_BONES]
JOINT_INDEX = {n: i for i, n in enumerate(BONE_NAMES)}

# The VISUAL skeleton (docs/CHARACTER-LOWPOLY.md c): CapturedRig's 22 names, order, parents and identity rest frames, with
# human rest offsets: a 1.70 m skull (about 7.25 heads), hips at .855 m (legs about 50 % of the height, the crotch at
# 47 %), shoulders at 1.36 m, the head centre about 1.58 m. The game's local rotations drive it 1:1 through the
# presentation adapter (root and pelvis height scaled by LEG_RATIO, foot and support-hand IK); the authoritative rig,
# hit volumes and weapon frames stay CapturedRig's.
LEG_RATIO = 1.5                       # visual hip height / game hip height (.855 / .57)
ROOT_HEIGHT = GAME_ROOT_HEIGHT * LEG_RATIO
VIS_OFFSETS = {
    'motion-root': (0, 0, 0), 'pelvis': (0, .91 - ROOT_HEIGHT, 0), 'spine': (0, .105, 0), 'chest': (0, .19, 0),
    'neck': (0, .225, 0), 'head': (0, .10, 0),
    'left-clavicle': (.13, .155, 0), 'left-shoulder': (.07, 0, 0), 'left-elbow': (0, -.30, 0), 'left-wrist': (0, -.25, 0),
    'left-hip': (.09, -.055, 0), 'left-knee': (0, -.40, 0), 'left-ankle': (0, -.38, 0), 'left-toe': (0, -.05, .13),
}
for _n in list(VIS_OFFSETS):
    if _n.startswith('left-'):
        _o = VIS_OFFSETS[_n]
        VIS_OFFSETS['right-' + _n[5:]] = (-_o[0], _o[1], _o[2])
OFFSET = VIS_OFFSETS


def rest_world(name, offsets=None, root=None):
    """Rest joint position in rig space (floor origin): visual by default, the game's with GAME_OFFSET."""
    offsets = offsets or OFFSET
    acc = Vector(offsets[name])
    par = PARENT[name]
    while par is not None:
        acc += Vector(offsets[par])
        par = PARENT[par]
    return acc + Vector((0, ROOT_HEIGHT if root is None else root, 0))


J = {n: rest_world(n) for n in BONE_NAMES}
JG = {n: rest_world(n, GAME_OFFSET, GAME_ROOT_HEIGHT) for n in BONE_NAMES}


def B(p):
    """Rig space -> Blender space."""
    return Vector((p[0], -p[2], p[1]))


# --------------------------------------------------------------------------------------------------------- regions/paints
# Hide regions (docs/CHARACTER-LOWPOLY.md d): each lies inside one ragdoll part. Death regions are the ragdoll part roots
# (NativeKillEffectRules.PartRoots), stored as the root joint's index.
HIDE_REGIONS = ['scalp', 'face', 'neck', 'chest', 'abdomen', 'back', 'pelvis',
                'upper-arm-left', 'forearm-left', 'hand-left', 'upper-arm-right', 'forearm-right', 'hand-right',
                'thigh-left', 'shin-left', 'foot-left', 'thigh-right', 'shin-right', 'foot-right']
HID = {n: i for i, n in enumerate(HIDE_REGIONS)}
PART_ROOTS = [1, 2, 5, 7, 8, 11, 12, 14, 15, 18, 19]
DEATH_NAMES = {1: 'pelvis', 2: 'torso', 5: 'head', 7: 'upper-arm-left', 8: 'forearm-left', 11: 'upper-arm-right',
               12: 'forearm-right', 14: 'thigh-left', 15: 'shin-left', 18: 'thigh-right', 19: 'shin-right'}
HIDE_DEATH = {'scalp': 5, 'face': 5, 'neck': 2, 'chest': 2, 'abdomen': 2, 'back': 2, 'pelvis': 1,
              'upper-arm-left': 7, 'forearm-left': 8, 'hand-left': 8, 'upper-arm-right': 11, 'forearm-right': 12,
              'hand-right': 12, 'thigh-left': 14, 'shin-left': 15, 'foot-left': 15, 'thigh-right': 18,
              'shin-right': 19, 'foot-right': 19}
# The task's coarse names for the same regions (head, neck, torso upper/lower, limbs), recorded in the manifest.
COARSE = {'scalp': 'head', 'face': 'head', 'neck': 'neck', 'chest': 'torso-upper', 'back': 'torso-upper',
          'abdomen': 'torso-upper', 'pelvis': 'torso-lower'}

# Paints: (name, default sRGB, channel). Channels as the doc's vertex paint (d): 0 fixed, 1 skin, 2 hair, 3 primary,
# 4 secondary, 5 accent glow, 6 core glow, 7 team. Tinting is by ratio, as the bank does (NativeBodyModels.GibColor):
# albedo = paint * param / default for the paint's channel, so a skin tone, hair colour or glow colour swap needs no new
# mesh. Channels 5 and 6 are emissive (the CA_Glow slot); their paint is the default glow colour.
SKIN = (0.84, 0.62, 0.49)
HAIR = (0.105, 0.105, 0.135)
CLOTH = (0.115, 0.125, 0.150)
GLOW_CYAN = (0.05, 0.78, 1.00)


def mul(c, f):
    return tuple(min(1.0, a * b) for a, b in zip(c, f if isinstance(f, tuple) else (f, f, f)))


PAINTS = [
    ('skin', SKIN, 1),
    ('skin-shade', mul(SKIN, (.80, .74, .74)), 1),
    ('lip-upper', mul(SKIN, (.80, .58, .56)), 1),
    ('lip-lower', mul(SKIN, (.90, .66, .62)), 1),
    ('mouth-line', mul(SKIN, (.36, .22, .22)), 1),
    ('nostril', mul(SKIN, (.90, .84, .82)), 1),
    ('eye-white', (0.93, 0.92, 0.90), 0),
    ('iris', (0.13, 0.21, 0.31), 0),
    ('pupil', (0.035, 0.035, 0.045), 0),
    ('eye-glint', (1.0, 1.0, 1.0), 0),
    ('lash', (0.06, 0.055, 0.065), 0),
    ('brow', mul(HAIR, .9), 2),
    ('hair', HAIR, 2),
    ('hair-deep', mul(HAIR, .55), 2),
    ('cloth', CLOTH, 4),
    ('cloth-band', mul(CLOTH, 2.3), 4),
    ('cloth-stripe', (0.10, 0.62, 0.72), 0),
    ('metal', (0.30, 0.33, 0.38), 0),
    ('metal-dark', (0.085, 0.095, 0.115), 0),
    ('metal-light', (0.62, 0.66, 0.72), 0),
    ('chip-body', (0.06, 0.075, 0.10), 0),
    ('chip-gold', (0.85, 0.62, 0.22), 0),
    ('chip-bezel', (0.78, 0.82, 0.88), 0),
    ('skin-light', mul(SKIN, (1.06, 1.06, 1.05)), 1),
    ('glow-core', GLOW_CYAN, 6),
    ('glow-device', GLOW_CYAN, 5),
    ('glow-chip', GLOW_CYAN, 5),
    ('glow-core-edge', mul(GLOW_CYAN, .62), 6),
    ('screen', (0.03, 0.10, 0.13), 0),
]
PID = {p[0]: i for i, p in enumerate(PAINTS)}
GLOW_CHANNELS = (5, 6)


def is_glow(paint):
    return PAINTS[paint][2] in GLOW_CHANNELS


# --------------------------------------------------------------------------------------------------------- mesh builder
class MB:
    """A plain vertex/face list in rig space with a region per vertex and a paint per face."""

    def __init__(self):
        self.V, self.R, self.F, self.W = [], [], [], []
        self.sharp = set()      # design creases: frozenset({a, b}) vertex pairs marked sharp

    def crease(self, row_a, row_b=None):
        """Mark the edges along a vertex row (or the rungs between two rows) sharp."""
        if row_b is None:
            for a, b in zip(row_a, row_a[1:]):
                self.sharp.add(frozenset((a, b)))
        else:
            for a, b in zip(row_a, row_b):
                self.sharp.add(frozenset((a, b)))

    def v(self, p, region, w=None):
        """w: optional explicit weights {joint name: weight} (rigid pieces and hair); None = from the armature."""
        self.V.append(Vector(p))
        self.R.append(region)
        self.W.append(w)
        return len(self.V) - 1

    def f(self, idx, paint=0):
        idx = tuple(idx)
        if len(set(idx)) == len(idx):
            self.F.append((idx, paint))

    def ring(self, pts, region):
        return [self.v(p, region) for p in pts]

    def strip(self, A, B, paint=0, closed=False):
        """Faces between two vertex rows of equal or different length (zipper by arc length)."""
        if len(A) == len(B):
            n = len(A)
            for i in range(n if closed else n - 1):
                j = (i + 1) % n
                self.f((A[i], A[j], B[j], B[i]), paint)
            return
        PA = [self.V[i] for i in A] + ([self.V[A[0]]] if closed else [])
        PB = [self.V[i] for i in B] + ([self.V[B[0]]] if closed else [])
        IA = list(A) + ([A[0]] if closed else [])
        IB = list(B) + ([B[0]] if closed else [])

        def params(P):
            d = [0.0]
            for k in range(1, len(P)):
                d.append(d[-1] + (P[k] - P[k - 1]).length)
            t = d[-1] or 1
            return [x / t for x in d]

        ta, tb = params(PA), params(PB)
        i = j = 0
        while i < len(IA) - 1 or j < len(IB) - 1:
            if j >= len(IB) - 1 or (i < len(IA) - 1 and ta[i + 1] <= tb[j + 1]):
                self.f((IA[i], IA[i + 1], IB[j]), paint)
                i += 1
            else:
                self.f((IA[i], IB[j + 1], IB[j]), paint)
                j += 1

    def cap(self, loop, centre, region, paint=0, flip=False):
        c = self.v(centre, region)
        n = len(loop)
        for i in range(n):
            a, b = loop[i], loop[(i + 1) % n]
            self.f((b, a, c) if not flip else (a, b, c), paint)
        return c

    def fan_open(self, row, centre_idx, paint=0):
        for i in range(len(row) - 1):
            self.f((row[i], row[i + 1], centre_idx), paint)

    def triangulate(self):
        """Split quads along their shorter diagonal and n-gons into fans (deterministic)."""
        out = []
        for idx, paint in self.F:
            if len(idx) == 3:
                out.append((idx, paint))
            elif len(idx) == 4:
                a, b, c, d = idx
                if (self.V[a] - self.V[c]).length <= (self.V[b] - self.V[d]).length + 1e-9:
                    out += [((a, b, c), paint), ((a, c, d), paint)]
                else:
                    out += [((a, b, d), paint), ((b, c, d), paint)]
            else:
                for k in range(1, len(idx) - 1):
                    out.append(((idx[0], idx[k], idx[k + 1]), paint))
        self.F = out

    def bvh(self):
        return BVHTree.FromPolygons([tuple(v) for v in self.V], [f for f, _ in self.F])


def align_loop(mb, A, Bl):
    """Rotate/reverse loop B so that B[i] corresponds to A[i] (minimum summed squared distance). Equal lengths."""
    n = len(A)
    best, bestB = None, None
    for rev in (False, True):
        L = list(reversed(Bl)) if rev else list(Bl)
        for k in range(n):
            R = L[k:] + L[:k]
            s = sum((mb.V[A[i]] - mb.V[R[i]]).length_squared for i in range(n))
            if best is None or s < best - 1e-12:
                best, bestB = s, R
    return bestB


def lerp(a, b, t):
    return a + (b - a) * t


def smooth(e0, e1, x):
    if e1 == e0:
        return 0.0 if x < e0 else 1.0
    t = max(0.0, min(1.0, (x - e0) / (e1 - e0)))
    return t * t * (3 - 2 * t)


def interp_table(table, key):
    """Piecewise-linear interpolation of rows (key, values...) at key."""
    keys = [r[0] for r in table]
    if key <= keys[0]:
        return list(table[0][1:])
    if key >= keys[-1]:
        return list(table[-1][1:])
    i = bisect.bisect_right(keys, key) - 1
    t = (key - keys[i]) / (keys[i + 1] - keys[i])
    return [lerp(a, b, t) for a, b in zip(table[i][1:], table[i + 1][1:])]


# ----------------------------------------------------------------------------------------------------------------- body
# Form reference: normalized cross-section shapes measured from the CC0 MakeHuman base mesh (sample_form_reference.py,
# form_reference.json). They give the torso and limb sections anatomical shapes (pecs, lats, spine groove, glutes,
# biceps, the forearm swell, the calf behind the shin); every size below (half-widths, depths, radii) is this model's
# own stylized design on the CapturedRig joints. No MakeHuman geometry is used.
with open(os.path.join(HERE, 'form_reference.json')) as _f:
    FORM = json.load(_f)
TORSO_THETA = [0, 22.5, 45, 67.5, 90, 112.5, 135, 157.5, 180]
# The reference's torso landmarks mapped to this body's heights on the CapturedRig joints.
OUR_MARKS = {'crotch': .470, 'hip': .570, 'pelvis': .625, 'navel': .745, 'ribs': .905, 'chest': .985,
             'shoulder': 1.10, 'neck': 1.25}


def _lerp_list(a, b, t):
    return [x + (y - x) * t for x, y in zip(a, b)]


def form_torso(y):
    """Normalized half-ring shape [(x/W, z/F|B)] at this body's height y (landmark-mapped into the reference)."""
    names = FORM['order']
    ours = [OUR_MARKS[n] for n in names]
    if y <= ours[0]:
        a = 0.0
    elif y >= ours[-1]:
        a = float(len(ours) - 1)
    else:
        i = bisect.bisect_right(ours, y) - 1
        a = i + (y - ours[i]) / (ours[i + 1] - ours[i])
    rows = FORM['torso']
    keys = [r['at'] for r in rows]
    a = min(max(a, keys[0]), keys[-1])
    i = max(0, min(len(rows) - 2, bisect.bisect_right(keys, a) - 1))
    t = (a - keys[i]) / ((keys[i + 1] - keys[i]) or 1)
    return [_lerp_list(p, q, t) for p, q in zip(rows[i]['shape'], rows[i + 1]['shape'])]


def form_limb(part, t, n, soften=1.0):
    """Normalized ring radii (mean 1) for a limb part at t along its bone, resampled to n, softened toward round."""
    rows = FORM[part]
    ts = [r['t'] for r in rows]
    t = min(max(t, ts[0]), ts[-1])
    i = max(0, min(len(rows) - 2, bisect.bisect_right(ts, t) - 1))
    u = (t - ts[i]) / ((ts[i + 1] - ts[i]) or 1)
    s = _lerp_list(rows[i]['shape'], rows[i + 1]['shape'], u)
    m = len(s)
    out = []
    for k in range(n):
        x = k * m / n
        j = int(x)
        f = x - j
        v = s[j % m] * (1 - f) + s[(j + 1) % m] * f
        out.append(1 + (v - 1) * soften)
    mean = sum(out) / n
    return [v / mean for v in out]


# Torso rows, crotch to neck base: y, half-width W, front depth F, back depth B, centre z, tweaks {column: (dr, dy)}.
# Column 0 is the front centre line, 8 the back centre line (TORSO_THETA). A V taper: lats .245 to a .150 waist.
TORSO = [
    (.490, .188, .098, .104, .000, {0: (0, -.004), 8: (-.012, 0)}),                       # leg split
    (.545, .198, .106, .132, .000, {8: (-.020, 0)}),                                      # glutes
    (.600, .193, .110, .122, .000, {8: (-.014, 0)}),
    (.640, .185, .113, .112, .000, {8: (-.008, 0)}),                                      # band bottom
    (.672, .177, .113, .105, .000, {8: (-.006, 0)}),                                      # band top
    (.676, .171, .109, .101, .000, {8: (-.006, 0)}),                                      # skin
    (.745, .150, .106, .094, .000, {0: (-.010, 0), 8: (-.010, 0)}),                       # navel, waist
    (.792, .155, .113, .097, .000, {0: (-.004, 0), 1: (.004, 0), 8: (-.010, 0)}),         # lower ab block
    (.835, .163, .114, .102, .000, {0: (-.010, 0), 1: (-.004, 0), 2: (-.006, 0), 8: (-.010, 0)}),  # ab groove
    (.877, .178, .121, .110, .000, {0: (-.004, 0), 1: (.004, 0), 8: (-.011, 0)}),         # upper ab block
    (.926, .204, .125, .124, .000, {0: (-.008, 0), 1: (-.004, 0), 2: (0, .010), 3: (0, .016), 8: (-.012, 0)}),  # rib line
    (.952, .220, .150, .130, .000, {0: (-.018, -.004), 2: (0, .006), 3: (-.006, .020), 4: (-.010, .010), 8: (-.012, 0)}),  # pec edge
    (1.006, .240, .160, .140, .000, {0: (-.018, 0), 3: (-.004, 0), 4: (-.012, 0), 8: (-.010, 0)}),  # pecs, lats
    (1.050, .245, .156, .144, .000, {0: (-.014, 0), 8: (-.010, 0)}),                      # socket bottom
    (1.112, .240, .138, .140, .000, {0: (-.008, 0), 8: (-.012, 0)}),                      # socket middle
    (1.168, .220, .108, .124, .000, {0: (-.006, -.004), 8: (-.008, .006)}),               # socket top
    (1.215, .182, .086, .104, -.006, {0: (-.004, -.030), 1: (0, -.022), 2: (0, -.006), 3: (0, .016), 4: (0, .030),
                                      5: (0, .030), 6: (0, .020), 7: (0, .006), 8: (-.002, -.002)}),          # trapezius
    (1.268, .066, .060, .064, -.008, {0: (0, -.044), 1: (0, -.036), 2: (0, -.012), 3: (0, .014), 4: (0, .026),
                                      5: (0, .026), 6: (0, .018), 7: (0, .006), 8: (0, .0)}),                # neck base
]
WAIST_LOW, WAIST_TOP = 3, 4
SOCKET_ROWS = (13, 14, 15)
SOCKET_COLS = (2, 3, 4, 5)
SOCKET_POS = {
    (13, 2): (.232, 1.050, .118), (13, 3): (.262, 1.030, .066), (13, 4): (.272, 1.026, -.006), (13, 5): (.258, 1.042, -.080),
    (14, 2): (.290, 1.114, .100), (14, 5): (.298, 1.112, -.094),
    (15, 2): (.300, 1.170, .068), (15, 3): (.330, 1.186, .030), (15, 4): (.334, 1.188, -.028), (15, 5): (.310, 1.174, -.070),
}

# Arm rings (left arm, hanging from the shoulder joint): y, centre x, centre z, mean radius, reference part and t, ring
# vertices, extra (out, front, in, back) scale. Taper: round deltoid, biceps belly, elbow plane, forearm swell, slim wrist.
SX = J['left-shoulder'].x
ARM = [
    (1.062, SX + .008, .000, .070, ('upperarm', .35), 10, (1.10, 1.0, .85, 1.0)),   # deltoid cap
    (1.004, SX + .004, .004, .060, ('upperarm', .48), 10, (1.06, 1.0, .92, 1.0)),   # deltoid insertion
    (.944, SX + .002, .006, .055, ('upperarm', .65), 10, (1.0, 1.06, 1.0, 1.0)),    # biceps
    (.884, SX, .005, .049, ('upperarm', .80), 10, (1.0, 1.0, 1.0, 1.0)),
    (.852, SX, .003, .045, ('upperarm', .92), 10, (1.0, .96, 1.0, 1.04)),           # elbow loops
    (.830, SX, .000, .044, ('upperarm', .92), 10, (1.0, .92, 1.0, 1.12)),           # elbow: the olecranon plane behind
    (.808, SX, .004, .046, ('forearm', .10), 8, (1.0, 1.0, 1.0, 1.04)),
    (.768, SX, .008, .050, ('forearm', .25), 8, (1.06, 1.0, 1.0, 1.0)),             # forearm swell
    (.712, SX, .006, .043, ('forearm', .45), 8, (1.0, 1.0, 1.0, 1.0)),
    (.655, SX, .003, .035, ('forearm', .68), 8, (.95, 1.05, .95, 1.05)),           # cuff zone (wholly wrist-weighted)
    (.606, SX, .000, .030, ('forearm', .88), 8, (.80, 1.22, .80, 1.18)),           # wrist: flat, wider front to back
    (.582, SX, .000, .030, ('forearm', .97), 8, (.80, 1.22, .80, 1.18)),
]
ARM_ELBOW_ROWS = (4, 5, 6)
ARM_CUFF_ROW = 9
ARM_N = 10
PALM_TIP_Y = .468

# Leg rings (left leg, straight down from the hip joint): y, centre x, centre z, mean radius, reference part and t,
# ring vertices, (out, front, in, back) scale. The boxer-briefs end at LEG_HEM (raised to .425 m).
HX = J['left-hip'].x
LEG = [
    (.455, HX + .010, -.004, .090, ('thigh', .25), 10, (1.0, 1.0, .95, 1.0)),
    (.425, HX + .008, -.002, .087, ('thigh', .35), 10, (1.0, 1.0, .95, 1.0)),     # boxer hem (cloth)
    (.421, HX + .008, -.002, .084, ('thigh', .36), 10, (1.0, 1.0, .95, 1.0)),     # hem lip (skin)
    (.380, HX + .004, .004, .077, ('thigh', .58), 10, (1.0, 1.04, 1.0, 1.0)),
    (.348, HX + .001, .008, .069, ('thigh', .80), 10, (1.0, 1.04, 1.0, 1.0)),     # knee loops
    (.322, HX, .012, .065, ('thigh', .95), 10, (1.0, 1.10, 1.0, .92)),           # knee: the kneecap plane
    (.296, HX, .008, .061, ('shin', .08), 8, (1.0, 1.0, 1.0, 1.0)),
    (.252, HX, -.006, .064, ('shin', .28), 8, (1.0, 1.0, 1.0, 1.0)),             # calf
    (.198, HX, -.004, .057, ('shin', .50), 8, (1.0, 1.0, 1.0, 1.0)),
    (.145, HX, -.002, .045, ('shin', .70), 8, (1.0, 1.0, 1.0, 1.0)),
    (.104, HX, .002, .038, ('shin', .88), 8, (1.08, 1.0, 1.12, 1.0)),            # ankle: the malleoli
]
LEG_HEM = 1
LEG_KNEE_ROWS = (4, 5, 6)
CROTCH = (0, .470, -.006)
FORM_SOFTEN = {'thigh': .8, 'shin': .7, 'upperarm': .8, 'forearm': .8}


def limb_ring2(y, cx, cz, r, form, n, scale, mirror=1):
    """A limb ring from the reference's normalized radii: phi 0 = outward (+x on the left), 90 = front (+z)."""
    part, t = form
    shape = form_limb(part, t, n, FORM_SOFTEN[part])
    so, sf, si, sb = scale
    pts = []
    for k in range(n):
        ph = 2 * math.pi * k / n
        c, s = math.cos(ph), math.sin(ph)
        k2 = so * max(c, 0) ** 2 + si * max(-c, 0) ** 2 + sf * max(s, 0) ** 2 + sb * max(-s, 0) ** 2
        rr = r * shape[k] * k2
        pts.append((mirror * (cx + rr * c), y, cz + rr * s))
    return pts


def limb_ring(n, y, cx, cz, ro, rf, ri, rb, phase=0.0):
    pts = []
    for k in range(n):
        ph = 2 * math.pi * k / n + phase
        c, s = math.cos(ph), math.sin(ph)
        r = ro * max(c, 0) ** 2 + ri * max(-c, 0) ** 2 + rf * max(s, 0) ** 2 + rb * max(-s, 0) ** 2
        pts.append((cx + r * c, y, cz + r * s))
    return pts


def limb_radius(table, y, phi):
    """Skin radius of a limb table at height y and angle phi (interpolated between its rings)."""
    rows = sorted(table, key=lambda r: r[0])
    ys = [r[0] for r in rows]
    y = min(max(y, ys[0]), ys[-1])
    i = max(0, min(len(rows) - 2, bisect.bisect_right(ys, y) - 1))
    u = (y - ys[i]) / ((ys[i + 1] - ys[i]) or 1)
    out = []
    for row in (rows[i], rows[i + 1]):
        yy, cx, cz, r, form, n, scale = row
        pts = limb_ring2(yy, 0, 0, r, form, 32, scale)
        k = int(round(phi / (2 * math.pi) * 32)) % 32
        out.append(math.hypot(pts[k][0], pts[k][2]))
    return out[0] + (out[1] - out[0]) * u


# Foot (left), rings along +z: z, top y, bottom y, half-width at top, half-width at bottom, centre x shift, arch lift.
# Ring order (8): top-inner, top-mid, top-outer, outer, bottom-outer, bottom-mid, bottom-inner, inner. A rounded heel,
# a raised inner arch, the instep and the ball; the toes are their own block (build_toes).
FOOT = [
    (-.082, .052, .016, .022, .020, .000, .000),
    (-.068, .082, .004, .036, .032, .000, .000),     # heel
    (-.040, .100, .000, .043, .039, .000, .000),
    (-.004, .110, .000, .048, .043, .002, .010),     # ankle front / arch
    (.036, .100, .000, .054, .049, .004, .016),      # instep / arch
    (.074, .080, .000, .060, .056, .006, .010),
    (.108, .058, .000, .064, .060, .008, .000),      # ball
    (.132, .046, .002, .062, .058, .008, .000),
]
FOOT_HOLE_ROWS = (1, 2, 3)


def foot_ring(z, ty, by, wt, wb, sx, arch):
    cx = HX + sx
    mid = (ty + by) / 2
    # inner side is -x (toward the body) for the left foot; the arch lifts the inner bottom edge
    return [
        (cx - wt * .62, ty, z), (cx, ty + .004, z), (cx + wt * .62, ty - .003, z),
        (cx + wt, mid + (ty - by) * .10, z), (cx + wb * .8, by, z), (cx, by + arch * .35, z), (cx - wb * .8, by + arch, z),
        (cx - wt, mid + (ty - by) * .18 + arch * .5, z),
    ]


def build_toes(mb):
    """The toe block on the left foot: a big toe and a block of four smaller toes, rigid on the toe joint."""
    tj = J['left-toe']
    # big toe (inner, -x) and the small toes, each a capped 6-sided tube from the ball forward
    finger(mb, [Vector((tj.x - .034, .030, .118)), Vector((tj.x - .037, .028, .152)), Vector((tj.x - .038, .023, .178))],
           [.020, .018, .015], region='foot', up=Vector((0, 1, 0)), aspect=.72)
    finger(mb, [Vector((tj.x + .014, .027, .118)), Vector((tj.x + .012, .025, .148)), Vector((tj.x + .008, .019, .168))],
           [.033, .029, .022], region='foot', up=Vector((0, 1, 0)), aspect=.50)


HEAD_THETA = [0, 12, 24, 36, 48, 61, 75, 90, 106, 123, 141, 160, 180]
# Neck and head rings: y and the radius from the head axis (x = 0, z = 0) at each HEAD_THETA. Feature lines sit on rows.
# Designed at a 1.72 m human scale, then fitted to the game head: chin at 1.35 m, crown 1.64 m (centre 1.50 m).
_HEAD_DESIGN = [
    (1.496, [.058, .058, .058, .058, .059, .060, .061, .062, .063, .064, .065, .066, .066]),   # top of neck
    (1.503, [.080, .079, .075, .069, .064, .062, .062, .062, .063, .065, .067, .068, .069]),   # under the chin
    (1.512, [.094, .092, .085, .076, .071, .068, .066, .065, .066, .068, .070, .071, .072]),   # chin
    (1.522, [.099, .097, .091, .083, .077, .073, .070, .069, .070, .072, .075, .077, .077]),   # jaw line, lower lip
    (1.5345, [.102, .100, .095, .088, .082, .078, .075, .074, .075, .077, .080, .082, .083]),  # mouth line
    (1.546, [.106, .104, .098, .091, .086, .082, .079, .078, .079, .081, .086, .090, .091]),   # nose base
    (1.559, [.113, .102, .097, .093, .089, .086, .082, .081, .082, .085, .092, .097, .098]),   # nose tip
    (1.574, [.111, .101, .097, .096, .095, .092, .086, .083, .084, .088, .097, .103, .104]),   # nose mid, cheekbones
    (1.590, [.108, .100, .094, .095, .096, .094, .088, .084, .086, .091, .101, .107, .108]),   # lower lid, cheekbones
    (1.604, [.105, .097, .089, .089, .091, .090, .087, .083, .087, .093, .103, .109, .110]),   # eye line
    (1.619, [.103, .097, .092, .092, .092, .090, .088, .084, .088, .094, .104, .110, .111]),   # upper lid
    (1.635, [.102, .101, .099, .097, .094, .091, .089, .085, .089, .095, .104, .110, .111]),   # brow ridge
    (1.657, [.095, .094, .093, .092, .091, .089, .088, .085, .088, .093, .101, .106, .107]),   # forehead
    (1.681, [.086, .086, .085, .084, .084, .083, .082, .080, .082, .085, .091, .095, .096]),
    (1.701, [.069, .069, .068, .068, .068, .067, .066, .066, .067, .069, .073, .076, .077]),
    (1.714, [.046, .046, .046, .045, .045, .045, .044, .044, .045, .046, .048, .050, .051]),
]
HEAD_Y0, HEAD_SY, HEAD_SX, HEAD_SZ = 1.350, 1.32, 1.30, 1.28
# A defined jaw angle and a narrower chin: rows widened only at the jaw angle (61-123 degrees); the neck row slimmed.
JAW_BOOST = {0: .80, 1: .96, 2: 1.02, 3: 1.07, 4: 1.06, 5: 1.04, 6: 1.02}


def _fit_head(y):
    return HEAD_Y0 + (y - 1.4995) * HEAD_SY


HEAD = [(_fit_head(y), list(radii)) for y, radii in _HEAD_DESIGN]
CROWN = (0, _fit_head(1.722), -.004 * HEAD_SZ)
HEAD_ROW = {'neck': 0, 'chin_under': 1, 'chin': 2, 'lip_low': 3, 'mouth': 4, 'nose_base': 5, 'nose_tip': 6,
            'nose_mid': 7, 'lid_low': 8, 'eye': 9, 'lid_up': 10, 'brow': 11}


def head_point(row, k):
    y, radii = HEAD[row]
    th = math.radians(HEAD_THETA[k])
    r = radii[k]
    if row == 0 or 61 <= HEAD_THETA[k] <= 123:
        r *= JAW_BOOST.get(row, 1.0)
    x = 0.0 if k in (0, len(HEAD_THETA) - 1) else r * math.sin(th) * HEAD_SX
    return (x, y, r * math.cos(th) * HEAD_SZ)


def torso_point(row, k):
    y, W, F, Bk, zc, tw = TORSO[row]
    xn, zn = form_torso(y)[k]
    x = 0.0 if k in (0, len(TORSO_THETA) - 1) else W * xn
    z = zc + (F if zn >= 0 else Bk) * zn
    dr, dy = tw.get(k, (0, 0))
    if dr:
        L = math.hypot(x, z - zc) or 1
        x += dr * x / L
        z += dr * (z - zc) / L
    return (x, y + dy, z)


def build_body():
    mb = MB()

    def V(p, prt):
        return mb.v(p, prt)  # the region field holds the part tag until regions are assigned

    rows = []
    for ri in range(len(TORSO)):
        row = []
        for k in range(len(TORSO_THETA)):
            p = SOCKET_POS.get((ri, k)) or torso_point(ri, k)
            row.append(V(p, 'torso'))
        rows.append(row)
    for ri in range(len(rows) - 1):
        for k in range(len(TORSO_THETA) - 1):
            if ri in SOCKET_ROWS[:-1] and k in SOCKET_COLS[:-1]:
                continue
            paint = PID['skin']
            if ri < WAIST_LOW:
                paint = PID['cloth']
            elif ri in (WAIST_LOW, WAIST_TOP):
                paint = PID['cloth-band']
            mb.f((rows[ri][k], rows[ri][k + 1], rows[ri + 1][k + 1], rows[ri + 1][k]), paint)
    # the band's thin cyan accent line: the band's middle faces on the front and sides
    # (a separate row would add a loop; the accent is a dye strip set into the band's lower row instead)
    r0, r1, r2 = SOCKET_ROWS
    c0, c1, c2, c3 = SOCKET_COLS
    socket = [rows[r0][c0], rows[r0][c1], rows[r0][c2], rows[r0][c3], rows[r1][c3],
              rows[r2][c3], rows[r2][c2], rows[r2][c1], rows[r2][c0], rows[r1][c0]]
    arm_rows = [[V(p, 'arm') for p in limb_ring2(*row)] for row in ARM]
    sj = J['left-shoulder']
    a1 = align_loop(mb, socket, arm_rows[0])
    cap_ring = []
    for i in range(ARM_N):
        p = (mb.V[socket[i]] + mb.V[a1[i]]) * .5
        d = p - Vector((sj.x - .03, sj.y - .015, sj.z))
        cap_ring.append(V(p + d.normalized() * .016, 'arm'))
    mb.strip(socket, cap_ring, PID['skin'], closed=True)
    mb.strip(cap_ring, a1, PID['skin'], closed=True)
    arm_rows[0] = a1
    prev = a1
    for ai in range(1, len(arm_rows)):
        r = align_closed(mb, prev, arm_rows[ai])
        mb.strip(prev, r, PID['skin'], closed=True)
        arm_rows[ai] = r
        prev = r
    # palm (continuing the 8-sided wrist): a believable hand, back of hand outward (+x), thumb side forward (+z)
    wrist = prev
    palm = [(.552, .004, .020, .050, .022, .046), (.516, .004, .019, .052, .020, .048), (.486, .003, .018, .052, .019, .047)]
    for (y, dz, ro, rf, ri_, rb) in palm:
        r = [V(p, 'hand') for p in limb_ring(8, y, SX - .002, dz, ro, rf, ri_, rb)]
        r = align_closed(mb, prev, r)
        mb.strip(prev, r, PID['skin'], closed=True)
        prev = r
    c = V((SX - .002, PALM_TIP_Y, .004), 'hand')
    for i in range(len(prev)):
        mb.f((prev[i], prev[(i + 1) % len(prev)], c), PID['skin'])

    # legs: the loop is the torso's bottom half ring plus the crotch point (10 vertices)
    crotch = V(CROTCH, 'torso')
    leg_loop = list(rows[0]) + [crotch]
    leg_rows = [[V(p, 'leg') for p in limb_ring2(*row)] for row in LEG]
    first = align_loop(mb, leg_loop, leg_rows[0])
    mb.strip(leg_loop, first, PID['cloth'], closed=True)
    leg_rows[0] = first
    prev = first
    for li in range(1, len(leg_rows)):
        r = align_closed(mb, prev, leg_rows[li])
        mb.strip(prev, r, PID['cloth'] if li <= LEG_HEM else PID['skin'], closed=True)
        leg_rows[li] = r
        prev = r
    ankle = prev
    # foot
    frows = [[V(p, 'foot') for p in foot_ring(*f)] for f in FOOT]
    h0, h1, h2 = FOOT_HOLE_ROWS
    for fi in range(len(frows) - 1):
        for k in range(8):
            if fi in (h0, h1) and k in (0, 1):
                continue
            k2 = (k + 1) % 8
            mb.f((frows[fi][k2], frows[fi][k], frows[fi + 1][k], frows[fi + 1][k2]), PID['skin'])
    hole = [frows[h0][0], frows[h0][1], frows[h0][2], frows[h1][2], frows[h2][2], frows[h2][1], frows[h2][0], frows[h1][0]]
    mb.strip(ankle, align_closed(mb, ankle, hole), PID['skin'], closed=True)
    fz0 = FOOT[0]
    mb.cap(frows[0], (HX, (fz0[1] + fz0[2]) / 2, fz0[0] - .010), 'foot', PID['skin'])
    fz1 = FOOT[-1]
    mb.cap(list(reversed(frows[-1])), (HX + fz1[5], (fz1[1] + fz1[2]) / 2 - .004, fz1[0] + .010), 'foot', PID['skin'])

    # neck and head half rings
    hrows = [[V(head_point(hi, k), 'head') for k in range(len(HEAD_THETA))] for hi in range(len(HEAD))]
    mb.strip(rows[-1], hrows[0], PID['skin'])
    for hi in range(len(hrows) - 1):
        mb.strip(hrows[hi], hrows[hi + 1], PID['skin'])
    crown = V(CROWN, 'head')
    mb.fan_open(hrows[-1], crown, PID['skin'])
    mb.crease(rows[11][:5])                 # the pecs' lower edge
    mb.crease(rows[8][:3])                  # ab groove
    mb.crease(rows[6][:3])                  # ab groove at the navel
    mb.crease(rows[0][:3])                  # boxer leg line at the front
    mb.crease(hrows[2][1:9])                # jaw line
    mb.crease(hrows[HEAD_ROW['brow']][1:7])  # brow ridge
    mb.triangulate()
    nose = set(hrows[HEAD_ROW['nose_base']][:2] + hrows[HEAD_ROW['nose_tip']][:2])
    mb.F = [(f, PID['nostril'] if set(f) <= nose else p) for f, p in mb.F]
    # the waistband's cyan accent: the band's lower row, front and side columns
    band = set(rows[WAIST_LOW][:6] + rows[WAIST_TOP][:6])
    lowband = set(rows[WAIST_LOW])
    mb.F = [(f, PID['cloth-stripe'] if p == PID['cloth-band'] and set(f) <= band and len(set(f) & lowband) == 2 and
             all(mb.V[i].y < TORSO[WAIST_LOW][0] + .012 for i in f) else p) for f, p in mb.F]
    build_face(mb)
    build_ears(mb)
    build_fingers(mb)
    build_toes(mb)
    mb.triangulate()
    return mb, {'torso': rows, 'head': hrows, 'arm': arm_rows, 'leg': leg_rows, 'foot': frows}


# ----------------------------------------------------------------------------------------------------------------- face
def poly_area(P):
    return sum(P[i][0] * P[(i + 1) % len(P)][1] - P[(i + 1) % len(P)][0] * P[i][1] for i in range(len(P))) / 2


def ccw(P):
    return list(P) if poly_area(P) >= 0 else list(reversed(P))


def clip_convex(subject, clipper):
    """Sutherland-Hodgman: subject polygon clipped by a convex CCW clipper (2D)."""
    out = list(subject)
    for i in range(len(clipper)):
        a, b = clipper[i], clipper[(i + 1) % len(clipper)]
        inp, out = out, []
        if not inp:
            break

        def inside(p):
            return (b[0] - a[0]) * (p[1] - a[1]) - (b[1] - a[1]) * (p[0] - a[0]) >= 0

        def cross(p, q):
            dx1, dy1 = q[0] - p[0], q[1] - p[1]
            dx2, dy2 = b[0] - a[0], b[1] - a[1]
            den = dx1 * dy2 - dy1 * dx2
            t = ((a[0] - p[0]) * dy2 - (a[1] - p[1]) * dx2) / den if abs(den) > 1e-15 else 0
            return (p[0] + dx1 * t, p[1] + dy1 * t)

        for k in range(len(inp)):
            p, q = inp[k], inp[(k + 1) % len(inp)]
            if inside(q):
                if not inside(p):
                    out.append(cross(p, q))
                out.append(q)
            elif inside(p):
                out.append(cross(p, q))
    return out


def circle(c, r, n, rot=0.0):
    return [(c[0] + r * math.cos(rot + 2 * math.pi * k / n), c[1] + r * math.sin(rot + 2 * math.pi * k / n)) for k in range(n)]


class Projector:
    """Places 2D face-plane points (rig x, y) on the head surface, lifted along the surface normal."""

    def __init__(self, mb):
        self.tree = mb.bvh()

    def front(self, x, y, lift):
        hit = self.tree.ray_cast(Vector((x, y, .5)), Vector((0, 0, -1)))
        if hit[0] is None:
            raise RuntimeError('face point missed the head: %r' % ((x, y),))
        n = hit[1] if hit[1].z > 0 else -hit[1]
        return hit[0] + n * lift


def add_flat(mb, proj, P, lift, paint, region='face', centre=None):
    """A flat overlay polygon (CCW seen from the front) projected on the face, as a fan round its centroid."""
    P = ccw(P)
    if len(P) < 3:
        return
    cx = centre if centre is not None else (sum(p[0] for p in P) / len(P), sum(p[1] for p in P) / len(P))
    c = mb.v(proj.front(cx[0], cx[1], lift), region)
    ring = [mb.v(proj.front(p[0], p[1], lift), region) for p in P]
    for i in range(len(ring)):
        mb.f((ring[i], ring[(i + 1) % len(ring)], c), paint)


def add_band(mb, proj, lower, upper, lift_lo, lift_hi, paint, region='face', lift_mid=None):
    """A strip between two polylines (left to right), optionally with a raised middle row (lift_mid)."""
    lo = [mb.v(proj.front(p[0], p[1], lift_lo), region) for p in lower]
    hi = [mb.v(proj.front(p[0], p[1], lift_hi), region) for p in upper]
    if lift_mid is not None:
        mid = [mb.v(proj.front((a[0] + b[0]) / 2, (a[1] + b[1]) / 2, lift_mid), region) for a, b in zip(lower, upper)]
        mb.strip(lo, mid, paint)
        mb.strip(mid, hi, paint)
    else:
        mb.strip(lo, hi, paint)


# Feature placement on the fitted head (rig space, left side; the mirror makes the right).
EYE_C = (.0430, 1.4885)
EYE_ALMOND = [(-.0195, -.0012), (-.0135, .0070), (-.0045, .0095), (.0065, .0090), (.0158, .0060), (.0224, .0016),
              (.0160, -.0044), (.0055, -.0066), (-.0062, -.0060), (-.0142, -.0037)]


def build_face(mb):
    proj = Projector(mb)
    ex, ey = EYE_C
    almond = [(ex + u, ey + v) for u, v in EYE_ALMOND]
    almond_ccw = ccw(almond)
    # sclera, iris, pupil, glint
    add_flat(mb, proj, almond, .0010, PID['eye-white'])
    iris = clip_convex(circle((ex + .0012, ey + .0008), .0080, 10), almond_ccw)
    add_flat(mb, proj, iris, .0016, PID['iris'], centre=(ex + .0012, ey + .0008))
    pupil = clip_convex(circle((ex + .0012, ey + .0010), .0038, 8), almond_ccw)
    add_flat(mb, proj, pupil, .0021, PID['pupil'], centre=(ex + .0012, ey + .0010))
    add_flat(mb, proj, circle((ex - .0016, ey + .0042), .0016, 4, math.pi / 4), .0026, PID['eye-glint'])
    # upper lash line: along the upper lid, thickening outwards, with a flick past the outer corner
    up = [almond[0]] + almond[1:6]
    up = up + [(ex + .0262, ey + .0046)]
    thick = [.0010, .0016, .0021, .0026, .0031, .0034, .0012]
    lash_lo = [(p[0], p[1] - .0003) for p in up]
    lash_hi = [(p[0] + (.0004 if i else 0), p[1] + t) for i, (p, t) in enumerate(zip(up, thick))]
    add_band(mb, proj, lash_lo, lash_hi, .0024, .0024, PID['lash'])
    # lower lash on the outer half, and the lid crease above
    low = [almond[6], almond[5]]
    add_band(mb, proj, [(p[0], p[1] - .0010) for p in [almond[7], almond[6], almond[5]]],
             [(p[0], p[1]) for p in [almond[7], almond[6], almond[5]]], .0018, .0018, PID['lash'])
    crease = [(ex - .012, ey + .0118), (ex - .003, ey + .0146), (ex + .008, ey + .0142), (ex + .018, ey + .0110)]
    add_band(mb, proj, crease, [(p[0], p[1] + .0013) for p in crease], .0008, .0008, PID['skin-shade'])
    # brow: a tapered, ridged strip
    brow_lo = [(.0140, 1.5218), (.0260, 1.5248), (.0390, 1.5270), (.0510, 1.5272), (.0620, 1.5250), (.0700, 1.5218)]
    th = [.0078, .0080, .0072, .0060, .0042, .0024]
    brow_hi = [(x + .0010 * i / 5, y + t) for i, ((x, y), t) in enumerate(zip(brow_lo, th))]
    add_band(mb, proj, brow_lo, brow_hi, .0012, .0012, PID['brow'], lift_mid=.0030)
    # lips: the left half from the centre line (x = 0) to the corner; the mirror closes them
    my = HEAD[HEAD_ROW['mouth']][0] + .0010
    xs = [0, .0058, .0124, .0190, .0246, .0282]
    line = [(x, my + (.0005 if x == 0 else 0) - .0008 * (x / .028) ** 2) for x in xs]
    upper = [(.0, my + .0062), (.0058, my + .0078), (.0124, my + .0070), (.0190, my + .0050), (.0246, my + .0024), (.0282, my + .0004)]
    lower = [(.0, my - .0100), (.0058, my - .0097), (.0124, my - .0084), (.0190, my - .0060), (.0246, my - .0030), (.0282, my - .0008)]
    add_band(mb, proj, [(x, y + .0007) for x, y in line], upper, .0012, .0006, PID['lip-upper'], lift_mid=.0030)
    add_band(mb, proj, lower, [(x, y - .0007) for x, y in line], .0006, .0012, PID['lip-lower'], lift_mid=.0036)
    add_band(mb, proj, [(x, y - .0009) for x, y in line], [(x, y + .0009) for x, y in line], .0016, .0016, PID['mouth-line'])
    return proj


def build_ears(mb):
    """An ear on the left side of the head: a C-shaped rim round a recessed bowl, its back sunk into the skull."""
    cy, cz = 1.457, -.016
    yb = 1.457
    # the head's half-width at the ear (from the eye-line row at 90-106 degrees)
    hx = max(head_point(HEAD_ROW['eye'], 7)[0], head_point(HEAD_ROW['nose_mid'], 7)[0])
    tilt = math.radians(14)
    outline = [(.000, .036), (-.012, .032), (-.019, .020), (-.020, .004), (-.016, -.012), (-.009, -.026), (.001, -.034),
               (.008, -.028), (.010, -.012), (.011, .006), (.010, .022), (.007, .032)]
    inner = [(-.003, .022), (-.010, .015), (-.011, .002), (-.008, -.010), (-.002, -.016), (.003, -.006), (.004, .010)]

    def place(u, v, out):
        # u: along -z (back), v: up; rotated back by tilt; out: lateral offset from the skull
        uu = u * math.cos(tilt) - v * math.sin(tilt)
        vv = u * math.sin(tilt) + v * math.cos(tilt)
        return (hx - .010 + out, cy + vv, cz + uu)

    back = [mb.v(place(u, v, 0.0), 'ear') for u, v in outline]
    rim = [mb.v(place(u, v, .020 - .004 * abs(v) / .036), 'ear') for u, v in outline]
    bowl = [mb.v(place(u, v, .013), 'ear') for u, v in inner]
    mb.strip(back, rim, PID['skin'], closed=True)
    # rim top surface to the bowl (zipper), bowl floor fan
    mb.strip(rim, bowl, PID['skin-shade'], closed=True)
    c = mb.v(place(-.004, .002, .011), 'ear')
    for i in range(len(bowl)):
        mb.f((bowl[i], bowl[(i + 1) % len(bowl)], c), PID['skin-shade'])


def finger(mb, pts, radii, n=6, region='finger', paint=None, up=Vector((1, 0, 0)), aspect=1.0):
    """A capped tube through joint points, rings perpendicular to the segments (aspect flattens it along `up`)."""
    paint = PID['skin'] if paint is None else paint
    rings = []
    for i, (p, r) in enumerate(zip(pts, radii)):
        p = Vector(p)
        d = (Vector(pts[min(i + 1, len(pts) - 1)]) - Vector(pts[max(i - 1, 0)])).normalized()
        a = (up - d * up.dot(d))
        if a.length < 1e-6:
            a = Vector((0, 0, 1)) - d * d.z
        a.normalize()
        b = d.cross(a)
        ring = []
        for k in range(n):
            ph = 2 * math.pi * (k + .5) / n
            ring.append(mb.v(p + (a * math.cos(ph) * aspect + b * math.sin(ph)) * r * (1.0 if k % 3 else .92), region))
        rings.append(ring)
    for i in range(len(rings) - 1):
        mb.strip(rings[i], rings[i + 1], paint, closed=True)
    d0 = (Vector(pts[1]) - Vector(pts[0])).normalized()
    d1 = (Vector(pts[-1]) - Vector(pts[-2])).normalized()
    mb.cap(list(reversed(rings[0])), Vector(pts[0]) - d0 * radii[0] * .5, region, paint)
    mb.cap(rings[-1], Vector(pts[-1]) + d1 * radii[-1] * .7, region, paint)


def build_fingers(mb):
    """Left hand: four finger blocks curled into a fist round the weapon grip, and a separated thumb lying over them.
    The palm faces -x (toward the body), the thumb side is +z. Everything rides the wrist (no finger bones in the rig)."""
    x0 = SX - .006
    base_y = PALM_TIP_Y + .012
    # z of each finger at the knuckles, lengths (proximal, middle, distal), radius
    specs = [(.036, (.034, .024, .019), .0118), (.012, (.037, .026, .020), .0122), (-.012, (.035, .025, .019), .0118),
             (-.034, (.029, .020, .016), .0104)]
    # the fist: down past the knuckles, then back under toward the palm (-x), the tips tucked up
    dirs = (Vector((-.25, -1, 0)), Vector((-1, -.15, 0)), Vector((-.55, .85, 0)))
    for z, lengths, r in specs:
        p = Vector((x0 + .004, base_y, z))
        pts = [p.copy()]
        for L, d in zip(lengths, dirs):
            p = p + d.normalized() * L
            pts.append(p.copy())
        finger(mb, pts, [r * 1.05, r, r * .94, r * .86], n=6, up=Vector((0, 0, 1)), aspect=.92)
    # thumb: from the palm's front edge near the wrist, forward and down, over the curled index finger
    t0 = Vector((SX - .014, .548, .040))
    t1 = t0 + Vector((-.22, -.62, .75)).normalized() * .036
    t2 = t1 + Vector((-.60, -.70, .38)).normalized() * .030
    t3 = t2 + Vector((-.85, -.45, .10)).normalized() * .020
    finger(mb, [t0, t1, t2, t3], [.0158, .0142, .0128, .0112], n=6, up=Vector((1, 0, 0)), aspect=.9)
    # the knuckle ridge: a slim bar across the top of the fingers' bases (reads as the fist's knuckles)
    finger(mb, [Vector((x0 + .012, base_y - .004, .048)), Vector((x0 + .012, base_y - .004, -.044))], [.0095, .0095], n=6,
           up=Vector((0, 1, 0)), aspect=.85)


# ----------------------------------------------------------------------------------------------------------------- hair
HAIR_ID = 'spiky-01'
HAIR_CENTRE = Vector((0, 1.500, -.006))
# Hairline per head column (HEAD_THETA index k, both sides): the first head row the cap covers.
HAIRLINE = [13, 13, 13, 13, 13, 12, 11, 9, 9, 8, 6, 5, 5]


class Rand:
    """Fixed-seed LCG (deterministic jitter)."""

    def __init__(self, seed):
        self.s = seed & 0xffffffff

    def __call__(self):
        self.s = (1664525 * self.s + 1013904223) & 0xffffffff
        return self.s / 4294967296.0

    def sym(self, a):
        return (self() * 2 - 1) * a


# Hair clumps: root (azimuth, elevation) and tip (azimuth, elevation) on the cap in degrees (+azimuth to the left,
# elevation past 90 runs over the top to the back), tip lift off the cap (m), width, thickness, chain name or None.
# A clump hugs the cap from root to tip and lifts away towards its point.
CLUMPS = [
    # fringe: pointed bangs over the forehead, swept to the character's right
    ((25, 60), (12, 16), .022, .076, .026, 'front-l'),
    ((5, 64), (-6, 12), .024, .082, .028, 'front-l'),
    ((-15, 62), (-25, 15), .022, .078, .027, 'front-r'),
    ((-36, 56), (-46, 20), .020, .070, .025, 'front-r'),
    ((44, 52), (38, 22), .020, .066, .024, 'front-l'),
    ((-55, 47), (-64, 22), .018, .062, .022, 'front-r'),
    # up and out from the top
    ((12, 70), (20, 90), .075, .078, .030, 'front-l'),
    ((-12, 70), (-22, 90), .075, .078, .030, 'front-r'),
    ((0, 84), (-6, 106), .080, .086, .031, None),
    ((32, 76), (42, 98), .078, .080, .030, None),
    ((-32, 76), (-44, 98), .078, .080, .030, None),
    ((60, 64), (80, 76), .068, .076, .028, 'side-l'),
    ((-60, 64), (-80, 76), .068, .076, .028, 'side-r'),
    ((100, 68), (116, 80), .064, .076, .028, 'back-l'),
    ((-100, 68), (-116, 80), .064, .076, .028, 'back-r'),
    ((148, 72), (158, 86), .060, .076, .028, 'back-l'),
    ((-148, 72), (-158, 86), .060, .076, .028, 'back-r'),
    ((180, 68), (180, 84), .056, .078, .028, 'back-l'),
    # sides: down and back over the tops of the ears
    ((70, 42), (96, 12), .024, .066, .024, 'side-l'),
    ((96, 46), (120, 16), .024, .066, .024, 'side-l'),
    ((-70, 42), (-96, 12), .024, .066, .024, 'side-r'),
    ((-96, 46), (-120, 16), .024, .066, .024, 'side-r'),
    # back: down to the nape
    ((130, 46), (140, 6), .028, .072, .026, 'back-l'),
    ((160, 48), (166, 2), .030, .074, .026, 'back-l'),
    ((-160, 48), (-166, 2), .030, .074, .026, 'back-r'),
    ((-130, 46), (-140, 6), .028, .072, .026, 'back-r'),
    ((180, 30), (180, -12), .026, .070, .025, 'back-r'),
    ((118, 24), (128, -6), .022, .064, .024, 'back-l'),
    ((-118, 24), (-128, -6), .022, .064, .024, 'back-r'),
    ((145, 62), (150, 24), .030, .078, .027, 'back-l'),
    ((-145, 62), (-150, 24), .030, .078, .027, 'back-r'),
    ((180, 58), (176, 18), .032, .080, .028, 'back-l'),
    ((112, 58), (122, 30), .026, .070, .025, 'back-l'),
    ((-112, 58), (-122, 30), .026, .070, .025, 'back-r'),
]
HAIR_CHAINS = ['front-l', 'front-r', 'side-l', 'side-r', 'back-l', 'back-r']
# Spring tuning per chain (spiky low-poly hair: stiff clumps, a small bounce and sway, never jelly). Units: stiffness and
# damping are the spring's natural frequency (Hz) and damping ratio toward the rigid-follow pose; gravity as a fraction
# of 0.981 m/s^2; drag a linear air drag (1/s) on the tip's world velocity; maxAngle the degrees a bone may leave its
# rigid direction; colliders in the head and neck joint frames (rig source frame).
SPRING = {
    'front-l': dict(stiffness=9.0, damping=.38, gravity=.25, drag=.25, maxAngle=14),
    'front-r': dict(stiffness=9.0, damping=.38, gravity=.25, drag=.25, maxAngle=14),
    'side-l': dict(stiffness=10.0, damping=.42, gravity=.2, drag=.2, maxAngle=12),
    'side-r': dict(stiffness=10.0, damping=.42, gravity=.2, drag=.2, maxAngle=12),
    'back-l': dict(stiffness=8.0, damping=.36, gravity=.3, drag=.3, maxAngle=16),
    'back-r': dict(stiffness=8.0, damping=.36, gravity=.3, drag=.3, maxAngle=16),
}


def hair_cols():
    """Full ring of head columns: (k, side) from the front round the left, the back, then the right."""
    n = len(HEAD_THETA)
    return [(k, 1) for k in range(n)] + [(k, -1) for k in range(n - 2, 0, -1)]


def build_hair(body_mb):
    """The spiky-01 hairstyle: a cap shell over the scalp plus faceted spike clumps; separate object, own weights."""
    mb = MB()
    rigid = {'head': 1.0}
    cols = hair_cols()
    nrows = len(HEAD)

    def cap_point(row, k, side, t):
        if row >= nrows:
            p = Vector(CROWN)
        else:
            x, y, z = head_point(row, k)
            p = Vector((x * side, y, z))
        d = (p - HAIR_CENTRE)
        d.normalize()
        return p + d * t

    def thick(row):
        return .007 + .020 * smooth(9, 15, row)

    grid = {}
    for ci, (k, side) in enumerate(cols):
        for row in range(min(HAIRLINE[k], nrows - 1), nrows):
            grid[(ci, row)] = mb.v(cap_point(row, k, side, thick(row)), 'hair', rigid)
    crown = mb.v(Vector(CROWN) + Vector((0, .018, 0)), 'hair', rigid)
    nc = len(cols)
    for ci in range(nc):
        cj = (ci + 1) % nc
        for row in range(nrows - 1):
            a, b, c, d = grid.get((ci, row)), grid.get((cj, row)), grid.get((cj, row + 1)), grid.get((ci, row + 1))
            q = [i for i in (a, b, c, d) if i is not None]
            if len(q) >= 3:
                mb.f(q, PID['hair'])
        mb.f((grid[(ci, nrows - 1)], grid[(cj, nrows - 1)], crown), PID['hair'])
    # skirt: the cap's lower edge tucks into the scalp
    for ci, (k, side) in enumerate(cols):
        cj = (ci + 1) % nc
        r0, r1 = min(HAIRLINE[k], nrows - 1), min(HAIRLINE[cols[cj][0]], nrows - 1)
        a, b = grid[(ci, r0)], grid[(cj, r1)]
        a2 = mb.v(cap_point(r0, k, side, -.002), 'hair', rigid)
        b2 = mb.v(cap_point(r1, cols[cj][0], cols[cj][1], -.002), 'hair', rigid)
        mb.f((a2, b2, b, a), PID['hair-deep'])
    mb.triangulate()
    tree = mb.bvh()
    body_tree = body_mb.bvh()
    rnd = Rand(0x5eed01)

    def on_cap(az, el, lift):
        a, e = math.radians(az), math.radians(el)
        n = Vector((math.sin(a) * math.cos(e), math.sin(e), math.cos(a) * math.cos(e)))
        hit = tree.ray_cast(HAIR_CENTRE + n * .5, -n, .5)
        if hit[0] is None:
            # below the hairline: the head itself (the body builder holds the left half; mirror for the right)
            m = -1 if n.x < 0 else 1
            nm = Vector((n.x * m, n.y, n.z))
            o = HAIR_CENTRE + nm * .5
            hit = body_tree.ray_cast(Vector((o.x, o.y, o.z)), -nm, .5)
            if hit[0] is None:
                raise RuntimeError('clump point missed the head: %r' % ((az, el),))
            hit = (Vector((hit[0].x * m, hit[0].y, hit[0].z)), Vector((hit[1].x * m, hit[1].y, hit[1].z)))
        nn = hit[1] if hit[1].dot(n) > 0 else -hit[1]
        return hit[0] + nn * lift, nn

    clumps = []
    for (r0, r1, lift, W, T, chain) in CLUMPS:
        lift *= 1 + rnd.sym(.08)
        path = []
        for sgm in (0.0, .40, .75, 1.0):
            az = r0[0] + (r1[0] - r0[0]) * sgm
            el = r0[1] + (r1[1] - r0[1]) * sgm
            p, nn = on_cap(az, el, -.010 if sgm == 0 else lift * sgm ** 1.6 + .004)
            path.append((p, nn))
        clumps.append((path, W, T, chain))
    groups = {c: [] for c in HAIR_CHAINS}
    for path, W, T, chain in clumps:
        if chain:
            groups[chain].append((path[0][0], path[-1][0]))
    chain_bones = []
    for c in HAIR_CHAINS:
        g = groups[c]
        root = sum((a for a, _, in g), Vector()) / len(g)
        tip = sum((b for _, b in g), Vector()) / len(g)
        chain_bones.append((c, root, (tip - root).normalized(), (tip - root).length * .5))
    bone_of = {c: ('sec-hair-%s-1' % c, 'sec-hair-%s-2' % c) for c in HAIR_CHAINS}
    sizes = [(1.0, 1.0, 5), (.80, .85, 4), (.45, .60, 3)]
    for path, W, T, chain in clumps:
        if chain:
            b1, b2 = bone_of[chain]
            wts = [rigid, {'head': .30, b1: .70}, {b1: .50, b2: .50}, {b2: .80, b1: .20}]
        else:
            wts = [rigid] * 4
        rings = []
        for k, (sw, st, nv) in enumerate(sizes):
            c, nn = path[k]
            t = (path[k + 1][0] - c).normalized()
            lat = t.cross(nn)
            lat.normalize()
            nrm = lat.cross(t).normalized()
            ring = []
            for q in range(nv):
                ph = 2 * math.pi * q / nv + (.4 if nv == 4 else 0)
                ring.append(mb.v(c + nrm * math.cos(ph) * T * st * .5 + lat * math.sin(ph) * W * sw * .5, 'hair', wts[k]))
            rings.append(ring)
        for k in range(len(rings) - 1):
            mb.strip(rings[k], align_closed(mb, rings[k], rings[k + 1]), PID['hair'], closed=True)
        tip = mb.v(path[-1][0], 'hair', wts[3])
        last = rings[-1]
        for q in range(len(last)):
            mb.f((last[q], last[(q + 1) % len(last)], tip), PID['hair'])
    return mb, chain_bones, bone_of


def align_closed(mb, A, Bl):
    """Rotate B (any length) to start nearest A[0], same turning direction as A."""
    best = None
    for rev in (False, True):
        L = list(reversed(Bl)) if rev else list(Bl)
        for k in range(len(L)):
            R = L[k:] + L[:k]
            s = sum((mb.V[A[int(i * len(A) / len(R))]] - mb.V[R[i]]).length_squared for i in range(len(R)))
            if best is None or s < best[0] - 1e-12:
                best = (s, R)
    return best[1]


# ------------------------------------------------------------------------------------------------------- core and device
CORE_AT = (.050, 1.040)   # rig x, y on the chest surface: the chest joint + (.05, .12), near the heart (doc e)


def frame_on_surface(tree, x, y):
    hit = tree.ray_cast(Vector((x, y, .6)), Vector((0, 0, -1)))
    n = hit[1] if hit[1].z > 0 else -hit[1]
    up = Vector((0, 1, 0)) - n * n.y
    up.normalize()
    right = up.cross(n)          # character's left (+x) for a front-facing surface
    return hit[0], n, up, right


def shield(scale_w, scale_h):
    """The core's angular shield outline (local s left, t up), CCW seen from the front: a V notch on top, flat
    shoulders and a pointed base."""
    half = [(0, .030), (.024, .030), (.048, .030), (.058, .016), (.052, -.008), (.034, -.034), (.015, -.052), (0, -.060)]
    full = half + [(-x, y) for x, y in reversed(half[1:-1])]
    return ccw([(x * scale_w, y * scale_h) for x, y in full])


def build_core(body_mb):
    """The chest core: a faceted shield housing sunk into the chest, a raised bevelled rim, a recessed glowing gem."""
    mb = MB()
    w = {'chest': 1.0}
    tree = body_mb.bvh()
    c, n, up, right = frame_on_surface(tree, *CORE_AT)

    def P(s, t, h):
        return c + right * s + up * t + n * h

    rings = []
    spec = [(1.05, -.010, 'metal-dark'), (1.05, .007, 'metal-dark'), (.98, .012, 'metal'), (.92, .0128, 'metal-light'),
            (.86, .012, 'metal'), (.74, .006, 'metal-dark'), (.68, .007, 'glow-core')]
    for sc, h, _ in spec:
        rings.append([mb.v(P(s, t, h), 'chest', w) for s, t in shield(sc, sc)])
    for i in range(len(rings) - 1):
        mb.strip(rings[i], rings[i + 1], PID['glow-core-edge' if spec[i + 1][2] == 'glow-core' else spec[i + 1][2]], closed=True)
    # the gem: star facets rising to a ridge
    gem = rings[-1]
    mid = [mb.v(P(s, t + .002, .013), 'chest', w) for s, t in shield(.38, .40)]
    for i in range(len(gem)):
        j = (i + 1) % len(gem)
        mb.f((gem[i], gem[j], mid[j], mid[i]), PID['glow-core-edge'])
    top = mb.v(P(0, -.004, .017), 'chest', w)
    for i in range(len(mid)):
        mb.f((mid[i], mid[(i + 1) % len(mid)], top), PID['glow-core'])
    return mb, (c, n, up, right)


# Right wrist device (doc e): on the dorsal side of the right forearm just above the wrist (the palm faces +x on the
# right hand, so its back faces -x). Everything is rigid on `right-wrist`; the lid and the chip ride the two optional
# presentation bones `sec-device-lid` and `sec-device-chip` (children of right-wrist).
DEV_X = J['right-wrist'].x          # -.345
DEV_Y0, DEV_Y1 = .598, .656         # housing span along the forearm
BAND_Y0, BAND_Y1 = .600, .650
BAY = dict(y0=.606, y1=.642, z0=-.0155, z1=.0155, depth=.0095)
LID_HINGE_Y = .646
LID_OPEN_DEG = 112.0
CHIP_W, CHIP_H, CHIP_T = .026, .031, .0040
CHIP_APPROACH = (Vector((-.040, .012, .006)), 28.0)   # chip offset (rig) and tilt (deg) before it seats


def forearm_radius(y, phi):
    """Right forearm skin radius at height y, angle phi (0 = outward -x, 90 = front +z): the arm table, mirrored."""
    return limb_radius(ARM, y, phi)


def build_device():
    dev, lid, chip = MB(), MB(), MB()
    w = {'right-wrist': 1.0}
    wl = {'sec-device-lid': 1.0}
    wc = {'sec-device-chip': 1.0}
    N = 12

    def band_pt(y, k, off):
        phi = 2 * math.pi * k / N
        r = forearm_radius(y, phi) + off
        return Vector((DEV_X - r * math.cos(phi), y, r * math.sin(phi)))   # outward is -x on the right arm

    # band: inner lip, outer face, top and bottom bevels
    rows = []
    for y, off in [(BAND_Y0, .0015), (BAND_Y0 + .003, .0075), (BAND_Y1 - .003, .0075), (BAND_Y1, .0015)]:
        rows.append([dev.v(band_pt(y, k, off), 'hand-right', w) for k in range(N)])
    paints = ['metal', 'metal-dark', 'metal']
    for i in range(3):
        dev.strip(rows[i], rows[i + 1], PID[paints[i]], closed=True)
    inner0 = [dev.v(band_pt(BAND_Y0, k, -.004), 'hand-right', w) for k in range(N)]
    inner1 = [dev.v(band_pt(BAND_Y1, k, -.004), 'hand-right', w) for k in range(N)]
    dev.strip(inner0, rows[0], PID['metal-dark'], closed=True)
    dev.strip(rows[3], inner1, PID['metal-dark'], closed=True)
    # accent glow ring round the band's middle (front and back only, leaving the housing side)
    # housing: a chamfered block on the dorsal side (-x)
    xb = DEV_X - .040          # embedded in the band
    xo = DEV_X - .0605         # outer face
    zc, hz = 0.0, .029
    ch = .004

    def box_ring(x, inset):
        y0, y1, z0, z1 = DEV_Y0 + inset, DEV_Y1 - inset, -hz + inset, hz - inset
        c = ch
        pts = [(y0 + c, z0), (y1 - c, z0), (y1, z0 + c), (y1, z1 - c), (y1 - c, z1), (y0 + c, z1), (y0, z1 - c), (y0, z0 + c)]
        return [Vector((x, y, z)) for y, z in pts]

    h_back = [dev.v(p, 'hand-right', w) for p in box_ring(xb, 0)]
    h_side = [dev.v(p, 'hand-right', w) for p in box_ring(xo + .0035, 0)]
    h_top = [dev.v(p, 'hand-right', w) for p in box_ring(xo, .0035)]
    dev.strip(h_back, h_side, PID['metal-dark'], closed=True)
    dev.strip(h_side, h_top, PID['metal-light'], closed=True)
    # the top face around the bay, the bay walls and floor
    by0, by1, bz0, bz1 = BAY['y0'], BAY['y1'], BAY['z0'], BAY['z1']
    bay_rim = [Vector((xo, y, z)) for y, z in [(by0, bz0), (by1, bz0), (by1, bz1), (by0, bz1)]]
    rim = [dev.v(p, 'hand-right', w) for p in bay_rim]
    floor = [dev.v(p + Vector((BAY['depth'], 0, 0)), 'hand-right', w) for p in bay_rim]
    # top face: connect the chamfered top ring (8) to the bay rim (4)
    dev.strip(h_top, align_closed(dev, h_top, rim), PID['metal'], closed=True)
    dev.strip(rim, floor, PID['glow-device'], closed=True)
    dev.f(tuple(floor), PID['metal-dark'])
    # side glow strips along the housing's front and back faces, and a small screen on the front chamfer
    for zs in (-1, 1):
        z = zs * (hz + .0004)
        q = [Vector((xo + .006, DEV_Y0 + .010, z)), Vector((xo + .006, DEV_Y1 - .010, z)),
             Vector((xo + .0095, DEV_Y1 - .010, z)), Vector((xo + .0095, DEV_Y0 + .010, z))]
        if zs < 0:
            q.reverse()
        dev.f([dev.v(p, 'hand-right', w) for p in q], PID['glow-device'])
    scr = [Vector((DEV_X - .050, .604, .0352)), Vector((DEV_X - .050, .650, .0352)), Vector((DEV_X - .041, .650, .0425)),
           Vector((DEV_X - .041, .604, .0425))]
    dev.f([dev.v(p, 'hand-right', w) for p in reversed(scr)], PID['screen'])
    # lid: a plate over the bay with a hex lens, hinged at its proximal edge (rest = closed)
    lz, ly0, ly1, lt = .0185, BAY['y0'] - .003, LID_HINGE_Y, .0032
    lo = [Vector((xo - .0002, y, z)) for y, z in [(ly0 + .003, -lz), (ly1, -lz), (ly1, lz), (ly0 + .003, lz), (ly0, lz - .003), (ly0, -lz + .003)]]
    lo_out = [p + Vector((-lt, 0, 0)) for p in lo]
    a = [lid.v(p, 'hand-right', wl) for p in lo]
    b = [lid.v(p, 'hand-right', wl) for p in lo_out]
    lid.strip(a, b, PID['metal-dark'], closed=True)
    # outer face with a hex lens in its middle
    lc = Vector((xo - .0002 - lt, (ly0 + ly1) / 2 - .001, 0))
    hexo = [lid.v(lc + Vector((-.0003, math.sin(t) * .0085, math.cos(t) * .0085)), 'hand-right', wl)
            for t in [math.pi / 2 + k * math.pi / 3 for k in range(6)]]
    lid.strip(b, align_closed(lid, b, hexo), PID['metal'], closed=True)
    hc = lid.v(lc + Vector((-.0018, 0, 0)), 'hand-right', wl)
    for k in range(6):
        lid.f((hexo[k], hexo[(k + 1) % 6], hc), PID['glow-chip'])
    lid.f(list(reversed(a)), PID['metal-dark'])
    # chip: the game's cartridge (NativeChipCartridge: shoulder cuts on top, small cuts below, a bezel, a hex crystal
    # window, a gold edge connector), seated flat in the bay with its connector toward the wrist (-y)
    out = [(u, v) for u, v in [(-.0075, .0155), (.0075, .0155), (.013, .010), (.013, -.0125), (.010, -.0155),
                               (-.010, -.0155), (-.013, -.0125), (-.013, .010)]]
    seat = Vector((xo + BAY['depth'] - CHIP_T - .0004, (by0 + by1) / 2, 0))

    def cp(u, v, h):
        return seat + Vector((-h, v, u))

    back_r = [chip.v(cp(u, v, 0), 'hand-right', wc) for u, v in out]
    face_r = [chip.v(cp(u, v, CHIP_T), 'hand-right', wc) for u, v in out]
    bez_r = [chip.v(cp(u * .78, v * .80 - .0006, CHIP_T + .0006), 'hand-right', wc) for u, v in out]
    chip.strip(back_r, face_r, PID['chip-bezel'], closed=True)
    chip.strip(face_r, bez_r, PID['chip-bezel'], closed=True)
    hx = [chip.v(cp(math.cos(t) * .0062, .0018 + math.sin(t) * .0062, CHIP_T + .0010), 'hand-right', wc)
          for t in [math.pi / 2 + k * math.pi / 3 for k in range(6)]]
    chip.strip(bez_r, align_closed(chip, bez_r, hx), PID['chip-body'], closed=True)
    gc = chip.v(cp(0, .0018, CHIP_T + .0028), 'hand-right', wc)
    for k in range(6):
        chip.f((hx[k], hx[(k + 1) % 6], gc), PID['glow-chip'])
    chip.f(list(reversed(back_r)), PID['chip-body'])
    gold = [cp(-.0085, -.0148, CHIP_T + .0007), cp(.0085, -.0148, CHIP_T + .0007), cp(.0085, -.0118, CHIP_T + .0007),
            cp(-.0085, -.0118, CHIP_T + .0007)]
    chip.f([chip.v(p, 'hand-right', wc) for p in gold], PID['chip-gold'])
    return dev, lid, chip, seat


# --------------------------------------------------------------------------------------------------------- blender glue
PART_CODES = ['torso', 'head', 'arm', 'hand', 'leg', 'foot', 'face', 'ear', 'finger', 'hair', 'piece']


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.unit_settings.system = 'METRIC'
    sc.unit_settings.scale_length = 1.0
    sc.render.fps = 30
    return sc


# Shading: big planes shade smooth inside and break crisply at their edges. Edges are sharp where the faces meet at more
# than SHARP_DEG, at paint borders, along the design creases and on flat pieces; features on the face stay flat.
SHARP_DEG = {'body': 21.0, 'hair': 34.0, 'piece': 0.0}
FLAT_PARTS = {'face', 'ear'}


def face_normal(mb, f):
    a, b, c = (mb.V[i] for i in f[:3])
    n = (b - a).cross(c - a)
    return n.normalized() if n.length > 1e-12 else Vector((0, 0, 1))


def sharp_pairs(mb, kind, mirror_x=False):
    thr = math.radians(SHARP_DEG[kind])
    normals = [face_normal(mb, f) for f, _ in mb.F]
    edge_faces = {}
    for fi, (f, _) in enumerate(mb.F):
        for a, b in zip(f, f[1:] + f[:1]):
            edge_faces.setdefault(frozenset((a, b)), []).append(fi)
    out = set(mb.sharp)
    for e, fs in edge_faces.items():
        if kind == 'piece':
            out.add(e)
        elif len(fs) == 2:
            f0, f1 = fs
            if normals[f0].angle(normals[f1], 0) > thr or mb.F[f0][1] != mb.F[f1][1]:
                out.add(e)
        elif len(fs) == 1 and mirror_x and all(abs(mb.V[i].x) < 1e-6 for i in e):
            n = normals[fs[0]]
            if math.acos(max(-1.0, min(1.0, 1 - 2 * n.x * n.x))) > thr:   # the angle to the face's mirror image
                out.add(e)
    return out


def to_mesh(name, mb, kind='body', mirror_x=False):
    """Blender mesh from a builder: face int attribute 'ca_paint', point int attribute 'ca_part' (PART_CODES index),
    smooth faces with sharp edges (see SHARP_DEG)."""
    me = bpy.data.meshes.new(name)
    me.from_pydata([B(p) for p in mb.V], [], [f for f, _ in mb.F])
    me.validate(clean_customdata=False)
    pa = me.attributes.new('ca_paint', 'INT', 'FACE')
    pa.data.foreach_set('value', [p for _, p in mb.F])
    pt = me.attributes.new('ca_part', 'INT', 'POINT')
    pt.data.foreach_set('value', [PART_CODES.index(r if r in PART_CODES else 'piece') for r in mb.R])
    flat = kind == 'piece'
    for poly, (f, _) in zip(me.polygons, mb.F):
        poly.use_smooth = not flat and not all(mb.R[i] in FLAT_PARTS for i in f)
    sharp = sharp_pairs(mb, kind, mirror_x)
    for e in me.edges:
        e.use_edge_sharp = frozenset(e.vertices) in sharp
    return me


def link(obj, coll=None):
    (coll or bpy.context.scene.collection).objects.link(obj)
    return obj


def apply_modifiers(obj):
    view = bpy.context.view_layer
    for o in view.objects:
        o.select_set(False)
    obj.select_set(True)
    view.objects.active = obj
    for m in list(obj.modifiers):
        bpy.ops.object.modifier_apply(modifier=m.name)


def mirrored_body(mb):
    me = to_mesh('CA_Body', mb, 'body', mirror_x=True)
    obj = link(bpy.data.objects.new('CA_Body', me))
    mod = obj.modifiers.new('Mirror', 'MIRROR')
    mod.use_axis = (True, False, False)
    mod.use_clip = True
    mod.use_mirror_merge = True
    mod.merge_threshold = 1e-4
    apply_modifiers(obj)
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    recalc_closed(bm)
    bm.to_mesh(obj.data)
    bm.free()
    return obj


def components(bm):
    seen, out = set(), []
    for f in bm.faces:
        if f.index in seen:
            continue
        stack, comp = [f], []
        seen.add(f.index)
        while stack:
            g = stack.pop()
            comp.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in seen:
                        seen.add(h.index)
                        stack.append(h)
        out.append(comp)
    return out


def recalc_closed(bm):
    """Outward normals for every closed shell; open overlays keep their authored (front-facing) winding."""
    bm.faces.ensure_lookup_table()
    bm.faces.index_update()
    for comp in components(bm):
        if all(e.is_manifold for f in comp for e in f.edges):
            bmesh.ops.recalc_face_normals(bm, faces=comp)


def mesh_stats(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    tris = sum(len(f.verts) - 2 for f in bm.faces)
    nonman = sum(1 for e in bm.edges if not e.is_manifold)
    bound = sum(1 for e in bm.edges if e.is_boundary)
    out = dict(verts=len(bm.verts), faces=len(bm.faces), tris=tris, nonmanifold=nonman, boundary=bound)
    bm.free()
    return out


# ----------------------------------------------------------------------------------------------------- armature/weights
NATURAL_TAIL = {
    'motion-root': (0, ROOT_HEIGHT + .12, 0), 'pelvis': (0, .50, 0), 'spine': J['chest'], 'chest': J['neck'],
    'neck': J['head'], 'head': (0, 1.62, 0),
    'left-clavicle': J['left-shoulder'], 'left-shoulder': J['left-elbow'], 'left-elbow': J['left-wrist'],
    'left-wrist': (J['left-wrist'].x, .45, 0), 'left-hip': J['left-knee'], 'left-knee': J['left-ankle'],
    'left-ankle': J['left-toe'], 'left-toe': (J['left-toe'].x, .03, .17),
}
for _n in list(NATURAL_TAIL):
    if _n.startswith('left-'):
        _t = NATURAL_TAIL[_n]
        NATURAL_TAIL['right-' + _n[5:]] = (-_t[0], _t[1], _t[2])


def secondary_bones(chain_bones, seat):
    """[(name, parent, rest joint (rig), tail (rig), info)] for the hair chains and the device's lid and chip."""
    out = []
    for c, root, dirn, L in chain_bones:
        b1, b2 = 'sec-hair-%s-1' % c, 'sec-hair-%s-2' % c
        out.append((b1, 'head', root, root + dirn * L, dict(kind='spring', chain=c)))
        out.append((b2, b1, root + dirn * L, root + dirn * 2 * L, dict(kind='spring', chain=c)))
    hinge = Vector((DEV_X - .0605 - .0032, LID_HINGE_Y, 0))
    out.append(('sec-device-lid', 'right-wrist', hinge, hinge + Vector((0, 0, .03)), dict(kind='device', part='lid')))
    out.append(('sec-device-chip', 'right-wrist', seat, seat + Vector((-.02, 0, 0)), dict(kind='device', part='chip')))
    return out


def build_armature(sec):
    arm = bpy.data.armatures.new('CA_Rig')
    obj = link(bpy.data.objects.new('CA_Rig', arm))
    arm.display_type = 'STICK'
    view = bpy.context.view_layer
    view.objects.active = obj
    bpy.ops.object.mode_set(mode='EDIT')
    for name in BONE_NAMES:
        eb = arm.edit_bones.new(name)
        eb.head = B(J[name])
        eb.tail = B(NATURAL_TAIL[name])
        eb.roll = 0
        eb.use_deform = name != 'motion-root'
    for name in BONE_NAMES:
        if PARENT[name]:
            arm.edit_bones[name].parent = arm.edit_bones[PARENT[name]]
    for name, parent, head, tail, info in sec:
        eb = arm.edit_bones.new(name)
        eb.head, eb.tail, eb.roll = B(head), B(tail), 0
        eb.parent = arm.edit_bones[parent]
    bpy.ops.object.mode_set(mode='OBJECT')
    for name, parent, head, tail, info in sec:
        for k, v in info.items():
            obj.data.bones[name]['ca_' + k] = v
        obj.data.bones[name]['ca_secondary'] = True
    return obj


def identity_frames(rig):
    """Turn every bone so its rest frame is the character frame (identity rest rotations, as CapturedRig): tail
    straight up rig +y (Blender +Z), roll 0. Joint positions and weights are unchanged."""
    view = bpy.context.view_layer
    view.objects.active = rig
    bpy.ops.object.mode_set(mode='EDIT')
    for eb in rig.data.edit_bones:
        L = max(.03, (eb.tail - eb.head).length)
        eb.tail = eb.head + Vector((0, 0, L))
        eb.roll = 0
    bpy.ops.object.mode_set(mode='OBJECT')


def rig_pos(v):
    """Blender -> rig space."""
    return Vector((v[0], v[2], -v[1]))


def side_name(name, x):
    if x < 0 and name.startswith('left-'):
        return 'right-' + name[5:]
    return name


def weights_rule(part, p, auto):
    """The cleaned weights for one body vertex: rig-space position p, its part tag and its automatic weights."""
    x, y, z = p
    s = 'left-' if x >= 0 else 'right-'
    if part in ('face', 'ear'):
        return {'head': 1.0}
    if part == 'head':
        return {'head': .45, 'neck': .55} if y < HEAD[0][0] + .002 else {'head': 1.0}
    if part in ('hand', 'finger'):
        return {s + 'wrist': 1.0}
    if part == 'arm':
        if y <= ARM[ARM_CUFF_ROW][0] + .001:
            return {s + 'wrist': 1.0}
        if y < .80:
            t = smooth(.66, .76, y)
            return {s + 'wrist': 1 - t, s + 'elbow': t}
        if y < .866:
            t = smooth(.80, .866, y)
            return {s + 'elbow': 1 - t, s + 'shoulder': t}
        if y < 1.0:
            return {s + 'shoulder': 1.0}
    if part == 'leg':
        if y < .13:
            t = smooth(.08, .13, y)
            return {s + 'ankle': 1 - t, s + 'knee': t}
        if y < .29:
            return {s + 'knee': 1.0}
        if y < .352:
            t = smooth(.29, .352, y)
            return {s + 'knee': 1 - t, s + 'hip': t}
        if y < .43:
            return {s + 'hip': 1.0}
    if part == 'foot':
        t = smooth(.072, .112, z)
        w = {s + 'ankle': 1 - t, s + 'toe': t}
        if y > .095 and z < .03:
            u = smooth(.095, .112, y) * .3
            w = {s + 'ankle': (1 - t) * (1 - u), s + 'toe': t * (1 - u), s + 'knee': u}
        return w
    # torso, upper shoulders, hips: automatic weights, kept to joints that can reach this part of the body
    allowed = {'pelvis', 'spine', 'chest', 'neck', s + 'clavicle', s + 'shoulder', s + 'hip'}
    if abs(x) < 1e-5:
        allowed |= {'right-clavicle', 'right-shoulder', 'right-hip'}
    w = {k: v for k, v in auto.items() if k in allowed and v > 1e-4}
    if not w:
        w = {'chest': 1.0}
    if part == 'arm':       # deltoid rows: shoulder and clavicle only, chest fading out
        w = {k: v for k, v in w.items() if k in (s + 'shoulder', s + 'clavicle', 'chest')}
        w[s + 'shoulder'] = w.get(s + 'shoulder', 0) + .5
    if part == 'leg':       # the thigh's top rows: hip and pelvis
        w = {k: v for k, v in w.items() if k in (s + 'hip', 'pelvis')}
        w[s + 'hip'] = w.get(s + 'hip', 0) + .4
    return w


def limit_normalize(w, n=4, quantum=1 / 255):
    items = sorted(w.items(), key=lambda kv: (-kv[1], kv[0]))[:n]
    items = [(k, v) for k, v in items if v > 1e-6]
    tot = sum(v for _, v in items)
    items = [(k, v / tot) for k, v in items]
    # quantize to 1/255 so the export round-trips exactly, keeping the sum at 1
    q = [(k, round(v / quantum)) for k, v in items]
    q = [(k, c) for k, c in q if c > 0] or [(items[0][0], 255)]
    diff = 255 - sum(c for _, c in q)
    q[0] = (q[0][0], q[0][1] + diff)
    return {k: c / 255 for k, c in q}


def auto_weights(body, rig):
    """Bone-heat weights on a copy of the body's main closed shell (overlays, ears and fingers left out)."""
    keep = {PART_CODES.index(p) for p in ('torso', 'head', 'arm', 'hand', 'leg', 'foot')}
    tmp = body.copy()
    tmp.data = body.data.copy()
    tmp.name = 'CA_HeatTmp'
    link(tmp)
    parts = [d.value for d in tmp.data.attributes['ca_part'].data]
    bm = bmesh.new()
    bm.from_mesh(tmp.data)
    vid = bm.verts.layers.int.new('ca_vid')
    for v in bm.verts:
        v[vid] = v.index
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if parts[v.index] not in keep], context='VERTS')
    bm.to_mesh(tmp.data)
    bm.free()
    view = bpy.context.view_layer
    for o in view.objects:
        o.select_set(False)
    tmp.select_set(True)
    rig.select_set(True)
    view.objects.active = rig
    bpy.ops.object.parent_set(type='ARMATURE_AUTO')
    ids = [d.value for d in tmp.data.attributes['ca_vid'].data]
    groups = {g.index: g.name for g in tmp.vertex_groups}
    out = {}
    for v in tmp.data.vertices:
        out[ids[v.index]] = {groups[g.group]: g.weight for g in v.groups if g.weight > 0}
    bpy.data.objects.remove(tmp)
    return out


def set_weights(obj, rig, weights):
    obj.vertex_groups.clear()
    for name in [b.name for b in rig.data.bones if b.use_deform]:
        obj.vertex_groups.new(name=name)
    vg = {g.name: g for g in obj.vertex_groups}
    for i, w in enumerate(weights):
        for name, val in w.items():
            vg[name].add([i], val, 'REPLACE')
    obj.parent = rig
    mod = obj.modifiers.new('Armature', 'ARMATURE')
    mod.object = rig


def part_root(joint):
    j = joint
    while j is not None and JOINT_INDEX.get(j, -1) not in PART_ROOTS:
        j = PARENT.get(j) or (SEC_PARENT.get(j) if 'SEC_PARENT' in globals() else None)
    return JOINT_INDEX[j]


def dominant(w):
    return sorted(w.items(), key=lambda kv: (-kv[1], kv[0]))[0][0]


def hide_region(part, p, w):
    x, y, z = p
    side = 'left' if x >= 0 else 'right'
    d = dominant(w)
    if part == 'hair':
        return 'scalp'
    if part in ('face',):
        return 'face'
    if part == 'ear':
        return 'scalp'
    if d == 'head' or d.startswith('sec-hair'):
        front = z > -.02 and y < 1.565 and abs(x) < .105
        return 'face' if front else 'scalp'
    if d == 'neck':
        return 'neck'
    if d == 'pelvis':
        return 'pelvis'
    if d in ('spine', 'chest') or d.endswith('clavicle'):
        if z < -.01:
            return 'back'
        return 'abdomen' if d == 'spine' else 'chest'
    if d.endswith('shoulder'):
        return 'upper-arm-' + side
    if d.endswith('elbow'):
        return 'forearm-' + side
    if d.endswith('wrist') or d.startswith('sec-device'):
        return 'hand-' + side
    if d.endswith('hip'):
        return 'thigh-' + side
    if d.endswith('knee'):
        return 'shin-' + side
    return 'foot-' + side


# ----------------------------------------------------------------------------------------------------------- materials
def srgb_to_linear(c):
    return tuple(((x + .055) / 1.055) ** 2.4 if x > .04045 else x / 12.92 for x in c)


def colour_layer(obj):
    """Face-corner colour 'Col': the paint's default sRGB colour, alpha = channel * 32 / 255 (exact in bytes)."""
    me = obj.data
    paint = [d.value for d in me.attributes['ca_paint'].data]
    ca = me.color_attributes.new('Col', 'BYTE_COLOR', 'CORNER')
    for poly in me.polygons:
        name, rgb, ch = PAINTS[paint[poly.index]]
        lin = srgb_to_linear(rgb)
        for li in poly.loop_indices:
            ca.data[li].color = (lin[0], lin[1], lin[2], ch * 32 / 255)
        poly.material_index = 1 if is_glow(paint[poly.index]) else 0
    me.color_attributes.active_color = ca
    me.color_attributes.render_color_index = me.color_attributes.find('Col')


def node(nt, kind, loc, **kw):
    n = nt.nodes.new(kind)
    n.location = loc
    for k, v in kw.items():
        setattr(n, k, v)
    return n


def make_materials():
    """CA_Body: vertex colour x ratio tint for skin (1), hair (2) and secondary/undergarment (4); CA_Glow: emissive,
    vertex colour x the object's ca_glow_tint x ca_glow_gain (core, device, lid lens and chip set their own)."""
    body = bpy.data.materials.new('CA_Body')
    body.use_nodes = True
    nt = body.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = node(nt, 'ShaderNodeOutputMaterial', (900, 0))
    bsdf = node(nt, 'ShaderNodeBsdfPrincipled', (650, 0))
    bsdf.inputs['Roughness'].default_value = .62
    nt.links.new(bsdf.outputs[0], out.inputs[0])
    col = node(nt, 'ShaderNodeVertexColor', (-700, 0), layer_name='Col')
    chn = node(nt, 'ShaderNodeMath', (-500, -200), operation='MULTIPLY')
    chn.inputs[1].default_value = 255 / 32
    nt.links.new(col.outputs['Alpha'], chn.inputs[0])
    rnd = node(nt, 'ShaderNodeMath', (-330, -200), operation='ROUND')
    nt.links.new(chn.outputs[0], rnd.inputs[0])
    cur = col.outputs['Color']
    x = -300
    for ch, label, default in ((1, 'SkinTone', SKIN), (2, 'HairColor', HAIR), (4, 'SecondaryColor', CLOTH)):
        param = node(nt, 'ShaderNodeRGB', (x, 300), label=label, name=label)
        param.outputs[0].default_value = (*srgb_to_linear(default), 1)
        ref = node(nt, 'ShaderNodeRGB', (x, 450), label=label + 'Default', name=label + 'Default')
        ref.outputs[0].default_value = (*srgb_to_linear(default), 1)
        div = node(nt, 'ShaderNodeVectorMath', (x + 150, 350), operation='DIVIDE')
        nt.links.new(param.outputs[0], div.inputs[0])
        nt.links.new(ref.outputs[0], div.inputs[1])
        mulv = node(nt, 'ShaderNodeVectorMath', (x + 300, 200), operation='MULTIPLY')
        nt.links.new(cur, mulv.inputs[0])
        nt.links.new(div.outputs[0], mulv.inputs[1])
        eq = node(nt, 'ShaderNodeMath', (x + 150, -250), operation='COMPARE')
        eq.inputs[1].default_value = ch
        eq.inputs[2].default_value = .25
        nt.links.new(rnd.outputs[0], eq.inputs[0])
        mix = node(nt, 'ShaderNodeMix', (x + 450, 100), data_type='RGBA')
        nt.links.new(eq.outputs[0], mix.inputs['Factor'])
        nt.links.new(cur, mix.inputs[6])
        nt.links.new(mulv.outputs[0], mix.inputs[7])
        cur = mix.outputs[2]
        x += 320
    nt.links.new(cur, bsdf.inputs['Base Color'])

    glow = bpy.data.materials.new('CA_Glow')
    glow.use_nodes = True
    nt = glow.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = node(nt, 'ShaderNodeOutputMaterial', (700, 0))
    bsdf = node(nt, 'ShaderNodeBsdfPrincipled', (450, 0))
    bsdf.inputs['Base Color'].default_value = (.02, .03, .04, 1)
    bsdf.inputs['Roughness'].default_value = .25
    nt.links.new(bsdf.outputs[0], out.inputs[0])
    col = node(nt, 'ShaderNodeVertexColor', (-500, 0), layer_name='Col')
    tint = node(nt, 'ShaderNodeAttribute', (-500, -200), attribute_type='OBJECT', attribute_name='ca_glow_tint')
    gain = node(nt, 'ShaderNodeAttribute', (-500, -400), attribute_type='OBJECT', attribute_name='ca_glow_gain')
    m1 = node(nt, 'ShaderNodeVectorMath', (-250, -100), operation='MULTIPLY')
    nt.links.new(col.outputs['Color'], m1.inputs[0])
    nt.links.new(tint.outputs['Vector'], m1.inputs[1])
    nt.links.new(m1.outputs[0], bsdf.inputs['Emission Color'])
    st = node(nt, 'ShaderNodeMath', (-250, -400), operation='MULTIPLY')
    st.inputs[1].default_value = 2.5
    nt.links.new(gain.outputs['Fac'], st.inputs[0])
    nt.links.new(st.outputs[0], bsdf.inputs['Emission Strength'])
    return body, glow


def piece_object(name, mb, rig, materials, region_part, glow_gain=1.0):
    mb.triangulate()
    me = to_mesh(name, mb, 'hair' if region_part == 'hair' else 'piece')
    obj = link(bpy.data.objects.new(name, me))
    colour_layer(obj)
    for m in materials:
        me.materials.append(m)
    weights = [limit_normalize(w) for w in mb.W]
    set_weights(obj, rig, weights)
    obj['ca_glow_gain'] = glow_gain
    obj['ca_glow_tint'] = (1.0, 1.0, 1.0)
    return obj, weights


SEC_PARENT = {}


def region_attrs(obj, weights, parts):
    hide, death = [], []
    for v, w, part in zip(obj.data.vertices, weights, parts):
        p = rig_pos(v.co)
        h = hide_region(part, p, w)
        hide.append(HID[h])
        death.append(part_root(dominant(w)))
    a = obj.data.attributes.new('ca_hide', 'INT', 'POINT')
    a.data.foreach_set('value', hide)
    b = obj.data.attributes.new('ca_death', 'INT', 'POINT')
    b.data.foreach_set('value', death)
    return hide, death


def main():
    reset_scene()
    mb, rows = build_body()
    core_mb, core_frame = build_core(mb)
    hair_mb, chain_bones, bone_of = build_hair(mb)
    dev_mb, lid_mb, chip_mb, seat = build_device()
    sec = secondary_bones(chain_bones, seat)
    SEC_PARENT.update({n: p for n, p, _, _, _ in sec})
    rig = build_armature(sec)
    body = mirrored_body(mb)
    auto = auto_weights(body, rig)
    codes = [d.value for d in body.data.attributes['ca_part'].data]
    parts = [PART_CODES[c] for c in codes]
    weights = [limit_normalize(weights_rule(part, rig_pos(v.co), auto.get(v.index, {})))
               for v, part in zip(body.data.vertices, parts)]
    set_weights(body, rig, weights)
    body_mat, glow_mat = make_materials()
    colour_layer(body)
    body.data.materials.append(body_mat)
    body.data.materials.append(glow_mat)
    region_attrs(body, weights, parts)
    pieces = [('CA_Hair', hair_mb, 'hair', 1.0), ('CA_Core', core_mb, 'core', 1.0), ('CA_WristDevice', dev_mb, 'device', .35),
              ('CA_WristLid', lid_mb, 'device', 0.0), ('CA_WristChip', chip_mb, 'device', 1.0)]
    objs = {'CA_Body': body}
    for name, pm, part, gain in pieces:
        obj, w = piece_object(name, pm, rig, (body_mat, glow_mat), part, gain)
        region_attrs(obj, w, ['hair' if part == 'hair' else 'piece'] * len(w))
        objs[name] = obj
    objs['CA_WristChip'].hide_render = True    # the chip shows only while a chip is inserted
    identity_frames(rig)
    for name, obj in objs.items():
        print('MESH', name, mesh_stats(obj))
    bpy.context.preferences.filepaths.save_version = 0   # no .blend1 backups
    bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND, compress=True)
    print('SAVED', OUT_BLEND)


if __name__ == '__main__':
    main()
