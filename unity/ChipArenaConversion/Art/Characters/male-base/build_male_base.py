"""Chip Arena low-poly male base (Phase B): rebuilds male_base.blend from nothing.

Run headless with Blender 4.2 LTS (from the repository root):
    blender -b --factory-startup --python-exit-code 1 \
        --python unity/ChipArenaConversion/Art/Characters/male-base/build_male_base.py -- [--out path.blend]

Deterministic: fixed tables, two measured form references (form_reference.json, head_reference.json; see the README) and a
fixed-seed LCG only, no timestamps, factory settings. This script is the editable source; male_base.blend is its output,
and export_male_base.py writes the FBX, GLB, binary mesh and JSON manifest from that file.

Modelling space ("rig space") is the game's CapturedRig source frame: +x the character's left, +y up, +z forward, metres,
origin on the floor under the motion root. Blender space is rig space turned so the character faces Blender -Y:
blender(x, y, z) = (x, -z_rig, y_rig). Unity's frame is rig space with x mirrored (docs/CHARACTER-LOWPOLY.md, b and c).
The skeleton is the visual skeleton (docs/CHARACTER-LOWPOLY.md c): CapturedRig.Bones' 22 joints (same names, order,
parents, identity rest frames) with human rest offsets (VIS_OFFSETS; the game's GAME_OFFSET is kept beside them), plus
optional secondary bones (sec-hair-* under `head`, the device's lid and chip under `right-wrist`) a runtime may ignore.
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

# The VISUAL skeleton (docs/CHARACTER-LOWPOLY.md c): CapturedRig's 22 names, order, parents and identity rest frames
# with human rest offsets: a 1.74 m figure of 7.5 heads, the hip joints at .887 m (the legs about half the height, the
# crotch at .815 m), the shoulder joints at 1.402 m and +-.18 m, the head pivot at 1.567 m under a head centred at
# 1.624 m. The game's local joint rotations drive it 1:1 through a presentation adapter (root height scaled by
# LEG_RATIO, foot IK to the game's ground contacts, the support hand to the weapon's grip). The authoritative rig, the
# hit volumes and the weapon frames stay CapturedRig's (GAME_OFFSET, GAME_ROOT_HEIGHT), unchanged.
ROOT_HEIGHT = 1.182                   # the visual motion-root: feet + 1.182 (pelvis offset -.24 as the game's)
VIS_OFFSETS = {
    'motion-root': (0, 0, 0), 'pelvis': (0, -.24, 0), 'spine': (0, .105, 0), 'chest': (0, .20, 0),
    'neck': (0, .22, 0), 'head': (0, .10, 0),
    'left-clavicle': (.122, .155, 0), 'left-shoulder': (.07, 0, 0), 'left-elbow': (0, -.305, 0), 'left-wrist': (0, -.25, 0),
    'left-hip': (.09, -.055, 0), 'left-knee': (0, -.42, 0), 'left-ankle': (0, -.40, 0), 'left-toe': (0, -.042, .135),
}
for _n in list(VIS_OFFSETS):
    if _n.startswith('left-'):
        _o = VIS_OFFSETS[_n]
        VIS_OFFSETS['right-' + _n[5:]] = (-_o[0], _o[1], _o[2])
# Leg length ratio, visual / game (hip to ankle: .82 / .50): the adapter scales the game's motion-root translation by it.
LEG_RATIO = (VIS_OFFSETS['left-knee'][1] + VIS_OFFSETS['left-ankle'][1]) / (GAME_OFFSET['left-knee'][1] +
                                                                             GAME_OFFSET['left-ankle'][1])
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
    ('lip-upper', mul(SKIN, (.90, .76, .74)), 1),
    ('lip-lower', mul(SKIN, (.95, .82, .79)), 1),
    ('mouth-line', mul(SKIN, (.36, .22, .22)), 1),
    ('nostril', mul(SKIN, (.90, .84, .82)), 1),
    ('eye-white', (0.93, 0.92, 0.90), 0),
    ('iris', (0.17, 0.11, 0.07), 0),
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
    ('scalp', mul(HAIR, 1.2), 2),
    ('lid-shade', mul(SKIN, (.86, .80, .80)), 1),
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
# A lean, athletic young man on the visual skeleton (1.74 m, 7.5 heads, legs about half the height, shoulders about two
# head heights across, a narrow waist and pelvis). Form reference: normalized cross-section shapes measured from the
# CC0 MakeHuman base mesh (sample_form_reference.py, form_reference.json, see the README) give the torso and limb
# sections anatomical shapes (pecs, lats, spine groove, glutes, biceps, the forearm swell, the calf behind the shin);
# every size below (half-widths, depths, radii) is this model's own design. No MakeHuman geometry is used.
with open(os.path.join(HERE, 'form_reference.json')) as _f:
    FORM = json.load(_f)
TORSO_THETA = [15.0 * k for k in range(13)]
def _lerp_list(a, b, t):
    return [x + (y - x) * t for x, y in zip(a, b)]


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


# The torso is designed, not measured: front, side and back profile curves (half-width W, front depth F and back depth B
# per row), rounded cross-sections (a superellipse whose roundness changes from the pelvis through the waist to the
# ribcage) and broad muscle forms laid over them (TORSO_FORMS). Rows follow the muscle flow rather than running level:
# the pecs' lower edge rises into the armpit and the deltoid, the waistband dips into a V along the iliac crests.
# Row: y, W, F, B, centre z, {theta: dy} (degrees round the half ring, 0 the front centre line, 180 the back; dy is
# interpolated between the anchors).
BAND_V = {0: -.014, 22.5: -.012, 45: -.006, 67.5: 0, 90: .004, 112.5: .004, 135: 0, 157.5: -.002, 180: -.004}
TORSO = [
    (.838, .150, .086, .098, .000, {0: -.004, 22.5: 0, 180: 0}),                       # leg split
    (.880, .160, .094, .118, .000, {}),                                                # glutes, hip
    (.922, .158, .098, .112, .002, {}),
    (.952, .152, .099, .101, .003, BAND_V),                                            # band (the V along the crests)
    (.980, .146, .098, .094, .004, BAND_V),                                            # band top
    (.984, .144, .097, .092, .004, BAND_V),                                            # skin
    (1.045, .133, .097, .084, .006, {}),                                               # waist
    (1.085, .137, .100, .086, .006, {}),
    (1.118, .143, .104, .090, .006, {}),
    (1.150, .151, .109, .095, .006, {}),
    (1.183, .160, .118, .100, .006, {22.5: 0, 45: .006, 67.5: .012, 90: .014, 112.5: 0}),             # lower ribs
    (1.208, .170, .128, .104, .006, {0: 0, 22.5: .004, 45: .014, 67.5: .026, 90: .030, 112.5: .018, 135: .008,
                                     157.5: 0}),                                       # the pecs' lower edge
    (1.262, .186, .140, .112, .004, {22.5: 0, 45: .010, 67.5: .020, 90: .024, 112.5: .016, 135: 0}),  # pecs, lats
    (1.330, .188, .134, .116, .002, {}),                                               # socket bottom (armpit)
    (1.392, .178, .116, .108, .000, {}),                                               # socket middle
    (1.440, .160, .088, .096, -.004, {0: -.010, 22.5: 0, 157.5: 0, 180: .004}),        # socket top (shoulder line)
    (1.468, .126, .070, .086, -.006, {0: -.032, 22.5: -.026, 45: -.010, 67.5: .010, 90: .018, 112.5: .020, 135: .016,
                                      157.5: .006, 180: -.002}),                       # trapezius
    (1.490, .066, .060, .066, -.006, {0: -.058, 22.5: -.048, 45: -.026, 67.5: -.006, 90: .002, 112.5: .004, 135: .004,
                                      157.5: .002, 180: 0}),                           # neck base
]
# Cross-section roundness (superellipse exponent) by height: a broad pelvis, an oval waist, a round ribcage.
TORSO_ROUND = [(.838, 2.35), (.95, 2.3), (1.045, 2.05), (1.18, 1.95), (1.26, 2.15), (1.44, 2.2), (1.49, 2.0)]
# Muscle forms: (radial amplitude m, theta centre, theta sigma, y centre, y sigma); negative amplitudes are grooves.
TORSO_FORMS = [
    (.020, 30, 18, 1.265, .055),     # pectorals, up to the clavicles
    (-.012, 0, 8, 1.240, .070),      # sternum
    (.008, 16, 12, 1.100, .060),     # rectus abdominis
    (-.008, 0, 7, 1.100, .080),      # linea alba
    (-.005, 15, 12, 1.118, .012),    # its tendinous lines, faint
    (-.004, 15, 12, 1.045, .012),
    (-.004, 45, 8, 1.120, .060),     # the abdominals' outer edge
    (.009, 80, 18, .990, .030),      # obliques over the iliac crest
    (.016, 112, 18, 1.250, .070),    # latissimus
    (.010, 145, 16, 1.340, .050),    # scapulae
    (-.012, 180, 10, 1.150, .250),   # spine
    (.020, 150, 25, .875, .035),     # glutes
]
WAIST_LOW, WAIST_TOP = 3, 4
SOCKET_ROWS = (13, 14, 15)
SOCKET_COLS = (3, 4, 5, 6, 7, 8)
# The arm socket: an ellipse round the arm's root on the side of the chest (rig y, z centre and radii). Its 14 points run
# from the front middle down, round the bottom to the back, and over the top, evenly spaced (angle 0 front, 90 top).
SOCK_C, SOCK_R = (1.388, -.004), (.054, .074)
SOCKET_LOOP = ([(14, SOCKET_COLS[0])] + [(13, c) for c in SOCKET_COLS] + [(14, SOCKET_COLS[-1])] +
               [(15, c) for c in reversed(SOCKET_COLS)])
SOCK_ANG = {cell: -360.0 * i / len(SOCKET_LOOP) for i, cell in enumerate(SOCKET_LOOP)}


def _socket_pos(a):
    r = math.radians(a)
    s, c = math.sin(r), math.cos(r)
    x = .160 + .015 * max(0.0, -s) - .007 * max(0.0, s) - .004 * c * c
    return (x, SOCK_C[0] + SOCK_R[0] * s, SOCK_C[1] + SOCK_R[1] * c)


SOCKET_POS = {k: _socket_pos(a) for k, a in SOCK_ANG.items()}

# Arm rings (left arm, hanging from the shoulder joint): y, centre x, centre z, mean radius, reference part and t, ring
# vertices, extra (out, front, in, back) scale. Taper: a round deltoid, the biceps, the elbow plane, the forearm swell,
# a slim wrist.
SX = J['left-shoulder'].x
ARM = [
    (1.380, SX + .006, .000, .060, ('upperarm', .35), 14, (.86, 1.02, .88, 1.0)),   # deltoid cap
    (1.312, SX + .004, .004, .047, ('upperarm', .48), 12, (.90, 1.0, .92, 1.0)),    # deltoid insertion
    (1.250, SX + .001, .006, .050, ('upperarm', .65), 12, (.90, 1.12, .98, 1.06)),  # biceps in front, triceps behind
    (1.185, SX, .004, .044, ('upperarm', .80), 12, (.94, 1.04, 1.0, 1.08)),
    (1.135, SX, .002, .040, ('upperarm', .92), 10, (1.0, .96, 1.0, 1.04)),          # elbow loops
    (1.102, SX, .000, .040, ('upperarm', .92), 10, (1.04, .92, 1.0, 1.12)),         # elbow: the olecranon plane behind
    (1.068, SX, .004, .044, ('forearm', .10), 8, (1.10, 1.02, 1.0, 1.04)),          # the forearm full near the elbow
    (1.022, SX, .006, .046, ('forearm', .25), 8, (1.12, 1.0, 1.04, 1.0)),
    (.965, SX, .005, .038, ('forearm', .45), 8, (1.0, 1.04, .98, 1.04)),
    (.910, SX, .003, .032, ('forearm', .68), 8, (.92, 1.10, .92, 1.08)),           # cuff zone (wholly wrist-weighted)
    (.866, SX, .000, .028, ('forearm', .88), 8, (.80, 1.22, .80, 1.18)),           # wrist: flat, wider front to back
    (.850, SX, .000, .028, ('forearm', .97), 8, (.80, 1.22, .80, 1.18)),
]
ARM_ELBOW_ROWS = (4, 5, 6)
DELTOID_R = .058
ARM_CUFF_ROW = 9
ARM_N = len(SOCKET_LOOP)            # 14: the socket, the deltoid cap rings and the arm's first ring
# The palm (8-sided, continuing the wrist): y, z shift, out, front, in, back radii. The back of the hand faces out (+x on
# the left), the thumb side forward (+z).
PALM = [(.834, .004, .016, .039, .014, .037), (.797, .004, .016, .045, .014, .043), (.762, .003, .014, .046, .013, .044)]
PALM_TIP_Y = .747

# Leg rings (left leg, straight down from the hip joint): y, centre x, centre z, mean radius, reference part and t,
# ring vertices, (out, front, in, back) scale. The boxer-briefs end at LEG_HEM, mid-thigh.
HX = J['left-hip'].x
LEG = [
    (.795, HX + .010, -.004, .090, ('thigh', .25), 14, (1.02, 1.04, .90, 1.02)),
    (.690, HX + .010, -.002, .085, ('thigh', .38), 12, (1.06, 1.08, .92, 1.02)),    # boxer hem (cloth)
    (.686, HX + .010, -.002, .082, ('thigh', .39), 12, (1.06, 1.08, .92, 1.02)),    # hem lip (skin)
    (.610, HX + .006, .004, .075, ('thigh', .58), 12, (1.06, 1.10, .96, 1.04)),     # quadriceps sweep, hamstrings
    (.540, HX + .002, .010, .064, ('thigh', .78), 12, (.98, 1.06, 1.06, .98)),      # the inner teardrop above the knee
    (.484, HX, .014, .058, ('thigh', .92), 10, (1.0, 1.04, 1.02, .94)),
    (.454, HX, .012, .057, ('thigh', .97), 10, (1.04, 1.10, 1.04, .92)),           # knee: the patella plane, its width
    (.415, HX, .006, .054, ('shin', .08), 8, (1.0, 1.0, 1.0, 1.0)),
    (.372, HX, -.010, .062, ('shin', .22), 8, (1.0, 1.0, 1.10, 1.12)),             # calf: high on the inside
    (.320, HX, -.010, .059, ('shin', .36), 8, (1.06, 1.0, 1.0, 1.10)),             # and lower on the outside
    (.250, HX, -.005, .048, ('shin', .52), 8, (1.0, 1.0, 1.0, 1.04)),
    (.170, HX, -.002, .038, ('shin', .74), 8, (1.0, 1.0, 1.0, 1.0)),
    (.112, HX, .002, .034, ('shin', .88), 8, (1.08, 1.0, 1.12, 1.0)),            # ankle: the malleoli
]
LEG_HEM = 1
LEG_KNEE_ROWS = (5, 6, 7)
CROTCH = (0, .815, -.006)
FORM_SOFTEN = {'thigh': .8, 'shin': .7, 'upperarm': .8, 'forearm': .8}


def limb_ring2(y, cx, cz, r, form, n, scale, mirror=1, phase=0.0):
    """A limb ring from the reference's normalized radii: phi 0 = outward (+x on the left), 90 = front (+z)."""
    part, t = form
    shape = form_limb(part, t, n, FORM_SOFTEN[part])
    so, sf, si, sb = scale
    pts = []
    for k in range(n):
        ph = 2 * math.pi * (k + phase) / n
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
    (-.076, .050, .016, .019, .017, .000, .000),
    (-.062, .078, .004, .030, .027, .000, .000),     # heel
    (-.037, .096, .000, .036, .032, .000, .000),
    (-.004, .106, .000, .039, .035, .002, .010),     # ankle front / arch
    (.034, .094, .000, .043, .039, .004, .016),      # instep / arch
    (.070, .074, .000, .048, .045, .006, .010),
    (.104, .055, .000, .051, .048, .008, .000),      # ball
    (.128, .044, .002, .050, .046, .008, .000),
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
    finger(mb, [Vector((tj.x - .027, .028, .114)), Vector((tj.x - .030, .026, .144)), Vector((tj.x - .031, .021, .168))],
           [.017, .015, .0125], region='foot', up=Vector((0, 1, 0)), aspect=.72)
    finger(mb, [Vector((tj.x + .011, .025, .114)), Vector((tj.x + .009, .023, .140)), Vector((tj.x + .005, .018, .158))],
           [.027, .023, .018], region='foot', up=Vector((0, 1, 0)), aspect=.50)


def torso_point(row, k):
    y, W, F, Bk, zc, curve = TORSO[row]
    n = interp_table(TORSO_ROUND, y)[0]
    t = math.radians(TORSO_THETA[k])
    sn, cs = math.sin(t), math.cos(t)
    x = 0.0 if k in (0, len(TORSO_THETA) - 1) else W * abs(sn) ** (2 / n)
    z = zc + (F if cs >= 0 else Bk) * math.copysign(abs(cs) ** (2 / n), cs)
    th = TORSO_THETA[k]
    bump = sum(a * math.exp(-((th - t0) / st) ** 2 - ((y - y0) / sy) ** 2) for a, t0, st, y0, sy in TORSO_FORMS)
    L = math.hypot(x, z - zc) or 1
    x += bump * x / L
    z += bump * (z - zc) / L
    dy = 0.0
    if curve:
        keys = sorted(curve)
        i = max(0, min(len(keys) - 2, bisect.bisect_right(keys, th) - 1))
        u = (th - keys[i]) / ((keys[i + 1] - keys[i]) or 1)
        dy = curve[keys[i]] + (curve[keys[i + 1]] - curve[keys[i]]) * min(max(u, 0.0), 1.0)
    return (x, y + dy, z)


# The boxer-briefs' cyan side trim: a slim piping strip down the outer seam, waistband to hem (rig y), lifted off the
# cloth; its vertices take the weights of the skin under them.
TRIM_Y = (.950, .694)
TRIM_W, TRIM_LIFT = .0055, .0012


def build_trim(mb):
    faces = [f for f, _ in mb.F if all(mb.R[i] in ('torso', 'leg') for i in f)]
    tree = BVHTree.FromPolygons([tuple(v) for v in mb.V], faces)
    rows = []
    n = 12
    for i in range(n + 1):
        y = TRIM_Y[0] + (TRIM_Y[1] - TRIM_Y[0]) * i / n
        row = []
        for dz in (-TRIM_W / 2, TRIM_W / 2):
            hit = tree.ray_cast(Vector((.4, y, dz - .002)), Vector((-1, 0, 0)), .5)
            if hit[0] is None:
                raise RuntimeError('trim missed the body at %r' % y)
            nrm = hit[1] if hit[1].x > 0 else -hit[1]
            row.append(mb.v(hit[0] + nrm * TRIM_LIFT, 'trim'))
        rows.append(row)
    for a, b in zip(rows, rows[1:]):
        mb.f((a[0], a[1], b[1], b[0]), PID['cloth-stripe'])


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
    socket = [rows[r][c] for r, c in SOCKET_LOOP]
    arm_rows = [[V(p, 'arm') for p in limb_ring2(*row, phase=.5 if i == 0 else 0.0)] for i, row in enumerate(ARM)]
    sj = J['left-shoulder']
    # the socket's top meets the arm ring's outer side, so the deltoid wraps over the shoulder: socket point i sits at
    # angle a = -360 i / N, the first arm ring's vertex k at phi = 360 (k + .5) / N, and phi = 90 - a pairs i with 3 + i
    a1 = [arm_rows[0][(3 + i) % ARM_N] for i in range(ARM_N)]
    ball = Vector((SX + .006, 1.372, 0.0))          # the deltoid's round cap over the shoulder joint: two rings
    caps = []                                       # between the socket and the arm, on a sphere round the joint
    for t in (.36, .72):
        caps.append([V(ball + (mb.V[socket[i]].lerp(mb.V[a1[i]], t) - ball).normalized() * DELTOID_R, 'arm')
                     for i in range(ARM_N)])
    mb.strip(socket, caps[0], PID['skin'], closed=True)
    mb.strip(caps[0], caps[1], PID['skin'], closed=True)
    mb.strip(caps[1], a1, PID['skin'], closed=True)
    arm_rows[0] = a1
    prev = a1
    for ai in range(1, len(arm_rows)):
        r = align_closed(mb, prev, arm_rows[ai])
        mb.strip(prev, r, PID['skin'], closed=True)
        arm_rows[ai] = r
        prev = r
    for (y, dz, ro, rf, ri_, rb) in PALM:
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

    # neck: the torso's top row, a mid ring, the head's lower edge
    edge, head_names = build_head(mb)
    mid = []
    for a, b in zip(rows[-1], edge):
        p = (mb.V[a] + mb.V[b]) * .5
        axis = Vector((0, p.y, .004))
        q = axis + (p - axis) * 1.02
        k = len(mid)
        if k in (1, 2):         # the sternocleidomastoids' planes, from behind the ears down to the clavicles
            q += (q - axis).normalized() * .006
        if abs(mb.V[a].x) < 1e-9:
            q.x = 0.0
        mid.append(V(q, 'torso'))
    mb.strip(rows[-1], mid, PID['skin'])
    mb.strip(mid, edge, PID['skin'])
    for i in edge:
        mb.R[i] = 'neck'
    mb.crease(rows[11][:6])                 # the pecs' lower edge, rising into the armpit
    mb.crease(rows[8][:3])                  # the abdominals' lines, across the rectus only
    mb.crease(rows[6][:3])
    mb.crease([rows[r][0] for r in range(6, 12)])   # the linea alba up to the sternum
    mb.triangulate()
    # the waistband's cyan accent: a tab at the front centre of the band
    tab = set(rows[WAIST_LOW][:3] + rows[WAIST_TOP][:3])
    mb.F = [(f, PID['cloth-stripe'] if p == PID['cloth-band'] and set(f) <= tab else p) for f, p in mb.F]
    build_trim(mb)
    build_ears(mb)
    build_fingers(mb)
    build_toes(mb)
    mb.triangulate()
    return mb, {'torso': rows, 'head': head_names, 'arm': arm_rows, 'leg': leg_rows, 'foot': frows}


# ----------------------------------------------------------------------------------------------------------------- head
# The head and face are a designed low-poly layout placed on a measured head form (head_reference.json: a radial distance
# field from a centre inside the skull, measured from the CC0 MakeHuman head, see the README). The layout follows
# established facial proportions at a 7.5-head figure: the eyes on the head's half line, one eye width between them, the
# brow, nose base and chin at the facial thirds, the mouth a third of the way from the nose base to the chin, the ears
# between the brow and the nose base. Every feature is geometry (lids round the eyeballs, a nose bridge, tip, alae and
# nostrils, lip borders and a mouth line, the jaw and chin planes); paint only colours what the geometry already shapes.
with open(os.path.join(HERE, 'head_reference.json')) as _f:
    HEADREF = json.load(_f)
HEAD_H = .232                 # chin to crown (1.74 m / 7.5)
CHIN_Y = 1.508                # the gnathion
HEAD_CZ = .050                # the head form's centre sits .05 m ahead of the neck and head joints
HC = Vector((0.0, CHIN_Y + HEADREF['centre_over_chin'] * HEAD_H, HEAD_CZ))
_HT, _HP, _HR = HEADREF['theta'], HEADREF['phi'], HEADREF['radius']


def head_dir(th, ph):
    t, p = math.radians(th), math.radians(ph)
    return Vector((math.sin(t) * math.cos(p), math.sin(p), math.cos(t) * math.cos(p)))


def _bump(th, ph, th0, ph0, sth, sph):
    return math.exp(-((th - th0) / sth) ** 2 - ((ph - ph0) / sph) ** 2)


def _blurred(R, sigma_cells):
    """The field blurred along theta and phi (a separable Gaussian; theta mirrors at the centre lines)."""
    k = [math.exp(-.5 * (d / sigma_cells) ** 2) for d in range(-3 * int(sigma_cells) - 1, 3 * int(sigma_cells) + 2)]
    h = len(k) // 2
    n_p, n_t = len(R), len(R[0])

    def at_t(row, j):
        if j < 0:
            j = -j
        if j >= n_t:
            j = 2 * (n_t - 1) - j
        return row[j]

    A = [[sum(k[q] * at_t(R[i], j + q - h) for q in range(len(k))) / sum(k) for j in range(n_t)] for i in range(n_p)]
    return [[sum(k[q] * A[min(max(i + q - h, 0), n_p - 1)][j] for q in range(len(k))) / sum(k) for j in range(n_t)]
            for i in range(n_p)]


_HRS = _blurred(_HR, 3.0)


def _skull_weight(th, ph):
    """Where the cranium's measured form gives way to its rounded (blurred) form: the back, top and upper sides."""
    return max(smooth(-25, 5, ph) * smooth(70, 110, th), smooth(40, 62, ph))


def head_style(th, ph):
    """Stylization on the measured form (head heights): a firmer jaw corner."""
    return .010 * _bump(th, ph, 74, -50, 14, 10)


def _grid(R, th, ph):
    th = min(max(th, _HT[0]), _HT[-1])
    ph = min(max(ph, _HP[0]), _HP[-1])
    a, b = (ph - _HP[0]) / 2.5, th / 2.5
    i, j = min(int(a), len(_HP) - 2), min(int(b), len(_HT) - 2)
    u, v = a - i, b - j
    return R[i][j] * (1 - u) * (1 - v) + R[i][j + 1] * (1 - u) * v + R[i + 1][j] * u * (1 - v) + R[i + 1][j + 1] * u * v


def head_r(th, ph):
    w = _skull_weight(th, ph)
    return _grid(_HR, th, ph) * (1 - w) + _grid(_HRS, th, ph) * w + head_style(th, ph)


def head_surface(th, ph):
    p = HC + head_dir(th, ph) * (head_r(th, ph) * HEAD_H)
    if th <= 0 or th >= 180:
        p.x = 0.0
    return p


def head_polar(p):
    d = p - HC
    return (math.degrees(math.atan2(abs(d.x), d.z)), math.degrees(math.asin(max(-1.0, min(1.0, d.y / d.length)))))


def _solve(target, axes, th, ph, fix_theta=False):
    """(theta, phi) where the head surface's coordinates `axes` (two of x 0, y 1, z 2) equal `target` (metres)."""
    for _ in range(60):
        p = head_surface(th, ph)
        e0, e1 = p[axes[0]] - target[0], p[axes[1]] - target[1]
        if abs(e0) + abs(e1) < 2e-7:
            break
        h = .01
        pp = head_surface(th, ph + h)
        if fix_theta:
            d = (pp[axes[1]] - p[axes[1]]) / h
            ph = min(max(ph - e1 / d, -80.0), 89.0) if abs(d) > 1e-12 else ph
            continue
        pt = head_surface(th + h, ph)
        a, b = (pt[axes[0]] - p[axes[0]]) / h, (pp[axes[0]] - p[axes[0]]) / h
        c, d = (pt[axes[1]] - p[axes[1]]) / h, (pp[axes[1]] - p[axes[1]]) / h
        det = a * d - b * c
        if abs(det) < 1e-14:
            break
        dth, dph = (d * e0 - b * e1) / det, (a * e1 - c * e0) / det
        m = max(abs(dth), abs(dph))
        s = 6.0 / m if m > 6.0 else 1.0
        th = min(max(th - dth * s, 0.0), 180.0)
        ph = min(max(ph - dph * s, -80.0), 89.0)
    p = head_surface(th, ph)
    if abs(p[axes[0]] - target[0]) + abs(p[axes[1]] - target[1]) > 2e-4:
        raise RuntimeError('head point did not solve: %r %r' % (axes, target))
    return th, ph


_SEED = []


def _seed(target, axes, facing):
    """A starting (theta, phi) for _solve: the nearest grid point, the one facing `facing` (2 = front, 0 = side)."""
    if not _SEED:
        for i in range(0, 145):
            for j in range(0, 136):
                th, ph = i * 1.25, -80 + j * 1.25
                _SEED.append((th, ph, head_surface(th, ph)))
    best = None
    for th, ph, p in _SEED:
        e = abs(p[axes[0]] - target[0]) + abs(p[axes[1]] - target[1])
        key = (0 if e < .004 else 1, -p[facing] if e < .004 else e)
        if best is None or key < best[0]:
            best = (key, th, ph)
    return best[1], best[2]


def face_angles(xn, yn):
    """The head surface point seen at (x, y) = (xn, yn) head heights from the head centre, from the front."""
    t = (xn * HEAD_H, HC.y + yn * HEAD_H)
    th, ph = _seed(t, (0, 1), 2)
    return _solve(t, (0, 1), 0.0 if xn == 0 else th, ph, xn == 0)


def side_angles(zn, yn):
    """The head surface point seen at (z, y) = (zn, yn) head heights from the head centre, from the left side."""
    t = (HC.z + zn * HEAD_H, HC.y + yn * HEAD_H)
    th, ph = _seed(t, (2, 1), 0)
    return _solve(t, (2, 1), th, ph)


# The eyeball (measured): centre and radius in head heights, as (theta, phi, r, radius).
_EB = HEADREF['landmarks']['eyeball']
EYEBALL_C = HC + head_dir(_EB[0], _EB[1]) * (_EB[2] * HEAD_H)
EYEBALL_R = _EB[3] * HEAD_H
LID = .0013                   # the lid margin's thickness over the eyeball


def on_eyeball(xn, yn, lift=0.0):
    x, y = xn * HEAD_H, HC.y + yn * HEAD_H
    dx, dy = x - EYEBALL_C.x, y - EYEBALL_C.y
    dz = math.sqrt(max(0.0, EYEBALL_R ** 2 - dx * dx - dy * dy))
    return Vector((x, y, EYEBALL_C.z + dz + lift))


# The layout (left half). Kinds: F = front-projected (xn, yn), S = side-projected (zn, yn), A = angles (theta, phi),
# M = lid margin (front-projected, held just over the eyeball), E = on the eyeball (front-projected). The last field is
# a radial push in metres (+ into the head, - outward) that sets lines into the form: the mouth line, the lid creases.
FACE_LAYOUT = [
    # centre line, hairline to chin
    ('c_hair', 'F', 0, .335, 0), ('c_fh2', 'F', 0, .230, 0), ('c_fh1', 'F', 0, .140, 0), ('c_glab', 'F', 0, .078, 0),
    ('c_nas', 'F', 0, .030, 0), ('c_br1', 'F', 0, -.040, 0), ('c_br2', 'F', 0, -.105, 0), ('c_tip', 'F', 0, -.163, 0),
    ('c_col', 'F', 0, -.200, 0), ('c_sn', 'F', 0, -.223, .0006), ('c_phil', 'F', 0, -.242, .0004),
    ('c_lipt', 'F', 0, -.262, 0), ('c_seam', 'F', 0, -.292, .0016), ('c_lipb', 'F', 0, -.334, 0),
    ('c_ment', 'F', 0, -.362, .0010), ('c_chin', 'F', 0, -.425, 0), ('c_gn', 'F', 0, -.488, 0),
    # eye: lid margin, iris, upper crease, lower lid line
    ('e0', 'M', .074, -.010, 0), ('e1', 'M', .097, .011, 0), ('e2', 'M', .131, .020, 0), ('e3', 'M', .166, .016, 0),
    ('e4', 'M', .196, .004, 0), ('e5', 'M', .168, -.015, 0), ('e6', 'M', .133, -.022, 0), ('e7', 'M', .098, -.019, 0),
    ('ic', 'E', .133, -.0015, 0),
    ('i0', 'E', .161, -.002, 0), ('i1', 'E', .153, .0155, 0), ('i2', 'E', .133, .0175, 0), ('i3', 'E', .115, .0135, 0),
    ('i4', 'E', .105, -.002, 0), ('i5', 'E', .113, -.0175, 0), ('i6', 'E', .133, -.0195, 0),
    ('i7', 'E', .153, -.0165, 0),
    ('pu0', 'E', .1435, -.0015, 0), ('pu1', 'E', .138, .0075, 0), ('pu2', 'E', .128, .0075, 0),
    ('pu3', 'E', .1225, -.0015, 0), ('pu4', 'E', .128, -.0105, 0), ('pu5', 'E', .138, -.0105, 0),
    ('u0', 'F', .070, .017, .0006), ('u1', 'F', .098, .037, .0009), ('u2', 'F', .133, .046, .0009),
    ('u3', 'F', .170, .041, .0009), ('u4', 'F', .205, .020, .0006),
    ('l0', 'F', .088, -.038, .0003), ('l1', 'F', .133, -.046, .0004), ('l2', 'F', .176, -.038, .0003),
    ('l3', 'F', .214, -.014, 0),
    # brow (a raised strip on the brow ridge) and forehead
    ('b0', 'F', .056, .068, -.0006), ('b1', 'F', .108, .080, -.0012), ('b2', 'F', .160, .084, -.0012),
    ('b3', 'F', .212, .076, -.0010), ('b4', 'F', .250, .055, -.0004),
    ('b5', 'F', .226, .092, -.0008), ('b6', 'F', .168, .109, -.0010), ('b7', 'F', .112, .107, -.0010),
    ('b8', 'F', .060, .095, -.0006),
    ('f0', 'F', .080, .155, 0), ('f1', 'F', .160, .160, 0), ('f2', 'F', .228, .140, 0), ('f3', 'F', .078, .250, 0),
    ('f4', 'F', .158, .245, 0), ('f5', 'F', .226, .210, 0),
    # hairline: forehead, temple recess and corner, sideburn, round the ear, nape
    ('h1', 'F', .078, .333, 0), ('h2', 'F', .148, .320, 0), ('h3', 'F', .198, .294, 0), ('h4', 'F', .222, .262, 0),
    ('h5', 'F', .258, .254, 0), ('h6', 'S', .200, .175, 0), ('h7', 'S', .150, .035, 0), ('h8', 'S', .085, -.020, 0),
    ('h9', 'S', .040, .070, 0), ('h10', 'S', -.050, .110, 0), ('h11', 'S', -.150, .060, 0),
    ('h12', 'S', -.200, -.080, 0), ('h13', 'S', -.260, -.200, 0), ('h14', 'A', 180, -30, 0),
    # temple and outer orbit
    ('t0', 'S', .300, .110, 0), ('t1', 'S', .290, .020, 0), ('t2', 'S', .255, -.050, 0),
    # nose: dorsum edge, side wall, ala, nostril
    ('d0', 'F', .030, .034, 0), ('d1', 'F', .023, -.040, 0), ('d2', 'F', .025, -.105, 0), ('d3', 'F', .038, -.152, 0),
    ('s0', 'F', .052, -.008, 0), ('s1', 'F', .057, -.070, 0), ('s2', 'F', .066, -.128, 0),
    ('a0', 'F', .072, -.156, .0004), ('a1', 'F', .081, -.188, 0), ('a2', 'F', .064, -.211, .0005),
    ('a3', 'F', .040, -.207, .0010), ('a4', 'F', .016, -.214, .0006), ('n0', 'F', .028, -.192, .0010),
    # cheeks and the nasolabial fold
    ('k0', 'F', .110, -.075, 0), ('k1', 'F', .176, -.080, 0), ('k2', 'F', .232, -.064, 0), ('k3', 'F', .114, -.140, 0),
    ('k4', 'F', .180, -.150, 0), ('k5', 'S', .200, -.150, 0), ('k6', 'F', .180, -.240, 0), ('k7', 'S', .160, -.270, 0),
    ('g0', 'F', .093, -.200, .0005), ('g1', 'F', .113, -.246, .0006), ('g2', 'F', .134, -.290, .0004),
    # mouth: upper lip border, mouth line, lower lip border, philtrum column, the chin crease
    ('v1', 'F', .024, -.256, 0), ('v2', 'F', .058, -.266, 0), ('v3', 'F', .090, -.283, .0004),
    ('m1', 'F', .042, -.291, .0016), ('m2', 'F', .080, -.294, .0018), ('m3', 'F', .108, -.300, .0022),
    ('w1', 'F', .045, -.329, 0), ('w2', 'F', .085, -.316, .0004), ('p1', 'F', .022, -.236, 0),
    ('x0', 'F', .052, -.360, .0008), ('x1', 'F', .105, -.342, .0004),
    # chin and jaw: the chin's front plane and corners, the jaw line to the angle, the ramus up to the ear lobe
    ('q1', 'F', .052, -.425, 0), ('q2', 'F', .094, -.432, 0), ('q3', 'F', .052, -.486, 0), ('q4', 'F', .098, -.474, 0),
    ('j0', 'F', .150, -.442, 0), ('j1', 'F', .200, -.392, 0), ('j2', 'S', .150, -.380, 0), ('j3', 'S', .050, -.330, 0),
    ('j4', 'S', .030, -.215, 0),
    # under the jaw
    ('y0', 'A', 0, -66, 0), ('y1', 'A', 24, -62, 0), ('y2', 'A', 48, -56, 0),
]
# The head's lower edge (the neck joins here): theta 0 .. 180 in TORSO_THETA steps, under the chin to the nape.
HEAD_EDGE_PHI = [-74, -71, -65, -60, -56, -48, -40, -35, -33]
# Polylines the triangulation must keep (open, or closed when the last name repeats the first).
FACE_LINES = [
    ['e0', 'e1', 'e2', 'e3', 'e4', 'e5', 'e6', 'e7', 'e0'], ['i0', 'i1', 'i2', 'i3', 'i4', 'i5', 'i6', 'i7', 'i0'],
    ['pu0', 'pu1', 'pu2', 'pu3', 'pu4', 'pu5', 'pu0'],
    ['e0', 'u0', 'u1', 'u2', 'u3', 'u4', 'e4'], ['e0', 'l0', 'l1', 'l2', 'l3', 'e4'],
    ['b0', 'b1', 'b2', 'b3', 'b4', 'b5', 'b6', 'b7', 'b8', 'b0'],
    ['c_hair', 'h1', 'h2', 'h3', 'h4', 'h5', 'h6', 'h7', 'h8', 'h9', 'h10', 'h11', 'h12', 'h13', 'h14'],
    ['c_nas', 'd0'], ['d0', 'd1', 'd2', 'd3', 'c_tip'], ['d0', 's0', 's1', 's2', 'a0'],
    ['a0', 'a1', 'a2', 'a3', 'a4', 'c_col'], ['a3', 'n0', 'a4'], ['d3', 'a0'],
    ['c_lipt', 'v1', 'v2', 'v3', 'm3'], ['c_seam', 'm1', 'm2', 'm3'], ['c_lipb', 'w1', 'w2', 'm3'],
    ['a1', 'g0', 'g1', 'g2', 'x1'], ['c_gn', 'q3', 'q4', 'j0', 'j1', 'j2', 'j3', 'j4'], ['q1', 'q2', 'q4'],
    ['c_sn', 'p1', 'v1'],
]
HAIRLINE_NAMES = next(line for line in FACE_LINES if line[0] == 'c_hair')
# Paint regions, first match wins: (paint, closed polygon of layout names in the chart).
FACE_PAINT = [
    ('pupil', ['pu0', 'pu1', 'pu2', 'pu3', 'pu4', 'pu5']),
    ('iris', ['i0', 'i1', 'i2', 'i3', 'i4', 'i5', 'i6', 'i7']),
    ('eye-white', ['e0', 'e1', 'e2', 'e3', 'e4', 'e5', 'e6', 'e7']),
    ('lid-shade', ['e0', 'e1', 'e2', 'e3', 'e4', 'u4', 'u3', 'u2', 'u1', 'u0']),
    ('nostril', ['a3', 'n0', 'a4']),
    ('brow', ['b0', 'b1', 'b2', 'b3', 'b4', 'b5', 'b6', 'b7', 'b8']),
    ('lip-upper', ['c_lipt', 'v1', 'v2', 'v3', 'm3', 'm2', 'm1', 'c_seam']),
    ('lip-lower', ['c_seam', 'm1', 'm2', 'm3', 'w2', 'w1', 'c_lipb']),
]
# The cranium and the back of the head: an angle grid, thinned where it meets the designed points.
HEAD_GRID_PHI = [-36, -24, -12, 0, 12, 24, 36, 48, 59, 70, 80]
HEAD_GRID_STEP = 13.0         # degrees of arc between grid points along a row
SCALP_MIN_DIST = 6.5          # degrees: grid points closer than this to a designed point are dropped


def _chart(th, ph):
    """The layout chart: a stereographic (conformal) map of the left half of the head's directions, centred on the
    left side (+x). The centre plane (front and back centre lines, the crown) is its unit circle."""
    d = head_dir(th, ph)
    return Vector((-d.z / (1 + d.x), d.y / (1 + d.x)))


def _polar_of(c):
    """(theta, phi) of a chart point (the inverse of _chart)."""
    u, v = c.x, c.y
    q = u * u + v * v
    dx, dy, dz = (1 - q) / (1 + q), 2 * v / (1 + q), -2 * u / (1 + q)
    return (math.degrees(math.atan2(dx, dz)), math.degrees(math.asin(max(-1.0, min(1.0, dy)))))


def _circle_arc(a, b, n):
    """Points on the chart's unit circle from angle a to b (degrees)."""
    return [Vector((math.cos(math.radians(a + (b - a) * k / n)), math.sin(math.radians(a + (b - a) * k / n))))
            for k in range(n + 1)]


def _angle(c):
    return math.degrees(math.atan2(c.y, c.x))


def _inside(pt, poly):
    x, y = pt
    c = False
    n = len(poly)
    for i in range(n):
        x1, y1 = poly[i]
        x2, y2 = poly[(i + 1) % n]
        if (y1 > y) != (y2 > y) and x < x1 + (y - y1) * (x2 - x1) / (y2 - y1):
            c = not c
    return c


def head_layout():
    """[(name, 3D point, chart point)] for the left half of the head, and the lower edge's names."""
    pts = []
    for name, kind, a, b, push in FACE_LAYOUT:
        if kind in ('F', 'M', 'E'):
            th, ph = face_angles(a, b)
        elif kind == 'S':
            th, ph = side_angles(a, b)
        else:
            th, ph = float(a), float(b)
        p = head_surface(th, ph)
        if kind == 'M':
            q = on_eyeball(a, b, LID)
            p = Vector((p.x, p.y, q.z))
        elif kind == 'E':
            p = on_eyeball(a, b)
        if push:
            p = p - (p - HC).normalized() * push
        pts.append((name, p, _chart(th, ph)))
    edge = []
    for k, th in enumerate(TORSO_THETA):          # the neck's columns
        j = min(int(th / 22.5), 7)
        u = th / 22.5 - j
        ph = HEAD_EDGE_PHI[j] * (1 - u) + HEAD_EDGE_PHI[j + 1] * u
        name = 'edge%d' % k
        pts.append((name, head_surface(th, ph), _chart(th, ph)))
        edge.append(name)
    designed = [head_dir(*_polar_of(c)) for _, _, c in pts]

    def below_edge(th, ph, pad):
        k = min(int(th / 22.5), 7)
        u = th / 22.5 - k
        return ph < HEAD_EDGE_PHI[k] * (1 - u) + HEAD_EDGE_PHI[k + 1] * u + pad

    for ph in HEAD_GRID_PHI:
        n = max(2, int(round(180 * math.cos(math.radians(ph)) / HEAD_GRID_STEP)))
        for k in range(n + 1):
            th = 180.0 * k / n
            d = head_dir(th, ph)
            if below_edge(th, ph, 5) or min(math.degrees(d.angle(q, 0)) for q in designed) < SCALP_MIN_DIST:
                continue
            pts.append(('g%d_%d' % (round(th), ph), head_surface(th, ph), _chart(th, ph)))
    pts.append(('crown', head_surface(0, 90), _chart(0, 90)))
    return pts, edge


def build_head(mb):
    """The head's left half into mb: returns its lower edge (9 vertices, front to back) for the neck."""
    from mathutils.geometry import delaunay_2d_cdt
    pts, edge = head_layout()
    index = {n: i for i, (n, _, _) in enumerate(pts)}
    coords = [c for _, _, c in pts]
    cons = []
    for line in FACE_LINES + [edge]:
        for a, b in zip(line, line[1:]):
            cons.append((index[a], index[b]))
    verts, edges, faces, orig_v, orig_e, orig_f = delaunay_2d_cdt(coords, cons, [], 0, 1e-6)
    out_to_in = []
    for ov in orig_v:
        if not ov:
            raise RuntimeError('the head layout has crossing lines (a new vertex appeared)')
        out_to_in.append(ov[0])
    vid = [mb.v(p, 'head') for _, p, _ in pts]
    polys = [(PID[paint], [coords[index[n]] for n in poly]) for paint, poly in FACE_PAINT]
    hairline = [coords[index[n]] for n in HAIRLINE_NAMES]
    # the scalp: inside the hairline, round over the crown along the centre plane (the chart's circle)
    scalp = hairline + _circle_arc(_angle(hairline[-1]), _angle(hairline[0]) + 360 * (_angle(hairline[0]) <
                                                                                          _angle(hairline[-1])), 24)
    edge_c = [coords[index[n]] for n in edge]
    below = edge_c + _circle_arc(_angle(edge_c[-1]), _angle(edge_c[0]) - 360 * (_angle(edge_c[0]) >
                                                                                 _angle(edge_c[-1])), 24)
    for f in faces:
        ids = [out_to_in[i] for i in f]
        cen = sum((coords[i] for i in ids), Vector((0, 0))) / 3
        if _inside(cen, below):
            continue
        paint = PID['skin']
        for pnt, poly in polys:
            if _inside(cen, poly):
                paint = pnt
                break
        else:
            if _inside(cen, scalp):
                paint = PID['scalp']
        a2, b2, c2 = (coords[i] for i in ids)
        if (b2 - a2).cross(c2 - a2) < 0:
            ids = [ids[0], ids[2], ids[1]]
        mb.f([vid[i] for i in ids], paint)
    for line in (['c_lipt', 'v1', 'v2', 'v3', 'm3'], ['c_seam', 'm1', 'm2', 'm3'], ['c_gn', 'q3', 'q4', 'j0', 'j1', 'j2', 'j3'],
                 ['b0', 'b1', 'b2', 'b3', 'b4'], ['e0', 'u0', 'u1', 'u2', 'u3', 'u4', 'e4'], ['d0', 'd1', 'd2', 'd3']):
        mb.crease([vid[index[n]] for n in line])
    return [vid[index[n]] for n in edge], {n: vid[index[n]] for n in index}

# ---------------------------------------------------------------------------------------------------------- ears, hands
# The ear (left): a C-shaped rim round a recessed bowl, its front edge sunk into the skull, the back of the rim standing
# off it. It sits between the brow and the nose base, behind the jaw's ramus, tilted back like the nose's bridge.
EAR_AT = (-.072, -.072)       # (zn, yn) of the ear's centre on the side of the head, head heights from the head centre
EAR_TILT = 14.0


def build_ears(mb):
    th, ph = side_angles(*EAR_AT)
    skull = head_surface(th, ph)
    cy, cz, hx = skull.y, skull.z, skull.x
    tilt = math.radians(EAR_TILT)
    k = .96                   # the outline below is in metres for a 1.0 scale
    outline = [(.000, .036), (-.012, .032), (-.019, .020), (-.020, .004), (-.016, -.012), (-.009, -.026), (.001, -.034),
               (.008, -.028), (.010, -.012), (.011, .006), (.010, .022), (.007, .032)]
    inner = [(-.003, .022), (-.010, .015), (-.011, .002), (-.008, -.010), (-.002, -.016), (.003, -.006), (.004, .010)]

    def place(u, v, out):
        u, v = u * k, v * k
        uu = u * math.cos(tilt) - v * math.sin(tilt)
        vv = u * math.sin(tilt) + v * math.cos(tilt)
        return (hx - .006 + out, cy + vv, cz + uu)

    back = [mb.v(place(u, v, 0.0), 'ear') for u, v in outline]
    rim = [mb.v(place(u, v, .006 + .40 * max(0.0, .010 - u) - .003 * abs(v) / .036), 'ear') for u, v in outline]
    bowl = [mb.v(place(u, v, .004 + .30 * max(0.0, .008 - u)), 'ear') for u, v in inner]
    mb.strip(back, rim, PID['skin'], closed=True)
    mb.strip(rim, bowl, PID['skin-shade'], closed=True)
    c = mb.v(place(-.004, .002, .006), 'ear')
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


# Fingers (left hand, relaxed): knuckle z, (proximal, middle, distal) lengths, radius. The palm faces -x (the body), the
# thumb side is +z; the fingers hang together with a gentle curl toward the palm. Everything rides the wrist (no finger
# bones in the rig).
FINGERS = [(.031, (.043, .026, .020), .0098), (.010, (.047, .029, .021), .0101), (-.011, (.044, .027, .020), .0096),
           (-.030, (.035, .021, .017), .0086)]
FINGER_CURL = (Vector((-.16, -1, 0)), Vector((-.42, -1, 0)), Vector((-.72, -1, 0)))


def build_fingers(mb):
    x0 = SX - .004
    base_y = PALM_TIP_Y + .010
    for z, lengths, r in FINGERS:
        p = Vector((x0, base_y, z))
        pts = [p.copy()]
        for L, d in zip(lengths, FINGER_CURL):
            p = p + d.normalized() * L
            pts.append(p.copy())
        finger(mb, pts, [r * 1.08, r, r * .93, r * .84], n=6, up=Vector((0, 0, 1)), aspect=.92)
    # thumb: from the palm's front edge near the wrist, forward and down, resting by the index finger
    t0 = Vector((SX - .010, .812, .033))
    t1 = t0 + Vector((-.30, -.55, .75)).normalized() * .037
    t2 = t1 + Vector((-.40, -.80, .40)).normalized() * .030
    t3 = t2 + Vector((-.45, -.85, .20)).normalized() * .024
    finger(mb, [t0, t1, t2, t3], [.0134, .0124, .0112, .0100], n=6, up=Vector((1, 0, 0)), aspect=.9)


# ----------------------------------------------------------------------------------------------------------------- hair
HAIR_ID = 'spiky-01'


class Rand:
    """Fixed-seed LCG (deterministic jitter)."""

    def __init__(self, seed):
        self.s = seed & 0xffffffff

    def __call__(self):
        self.s = (1664525 * self.s + 1013904223) & 0xffffffff
        return self.s / 4294967296.0

    def sym(self, a):
        return (self() * 2 - 1) * a


# Hair clumps on the scalp: root and tip as (signed theta, phi) head angles (+theta the character's left, phi past 90
# runs over the crown to the back), the tip's lift off the cap (m), width, ridge height, spring chain or None. Short dark
# hair in swept clumps that lie on the head like shingles: up and back over the top with a swept quiff at the front,
# shorter and flatter down the sides and the back. Each clump is a faceted leaf, its edges on the cap, its ridge raised,
# its point lifting off.
CLUMPS = [
    # front: spikes rising from the hairline, up and back, leaning to the character's right
    ((0, 57), (-8, 76), .034, .062, .012, 'front-r'),
    ((18, 56), (8, 75), .032, .060, .012, 'front-l'),
    ((-20, 56), (-30, 74), .032, .060, .012, 'front-r'),
    ((36, 54), (26, 71), .026, .056, .011, 'front-l'),
    ((-38, 54), (-48, 70), .024, .054, .011, 'front-r'),
    # top: up and over
    ((10, 70), (4, 94), .028, .066, .012, None),
    ((-14, 70), (-22, 93), .026, .064, .012, None),
    ((44, 66), (52, 88), .020, .060, .011, 'side-l'),
    ((-46, 66), (-56, 88), .020, .060, .011, 'side-r'),
    # crown: back
    ((6, 88), (10, 112), .018, .064, .011, 'back-l'),
    ((-20, 88), (-26, 110), .016, .062, .011, 'back-r'),
    # sides: short, tapered and tight to the skull, down and back
    ((78, 60), (100, 44), .003, .050, .008, 'side-l'),
    ((-78, 60), (-100, 44), .003, .050, .008, 'side-r'),
    ((102, 66), (122, 48), .003, .050, .008, 'side-l'),
    ((-102, 66), (-122, 48), .003, .050, .008, 'side-r'),
    # back: tight, down to the nape
    ((140, 72), (146, 36), .005, .060, .009, 'back-l'),
    ((-140, 72), (-146, 36), .005, .060, .009, 'back-r'),
    ((168, 64), (170, 22), .005, .060, .009, 'back-l'),
    ((-168, 64), (-170, 22), .005, .060, .009, 'back-r'),
    ((124, 46), (128, 14), .004, .050, .008, 'back-l'),
    ((-124, 46), (-128, 14), .004, .050, .008, 'back-r'),
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
HAIR_CAP = (.0006, .009)      # the cap's thickness at the hairline and on top of the head (m)


def _cap_thickness(p):
    return HAIR_CAP[0] + (HAIR_CAP[1] - HAIR_CAP[0]) * smooth(HC.y + .10 * HEAD_H, HC.y + .42 * HEAD_H, p.y)


def hair_angles_point(th, ph):
    """The scalp point and its outward direction at signed head angles (phi over 90 continues over the crown)."""
    if ph > 90:
        ph = 180 - ph
        th = (180 - abs(th)) * (1 if th >= 0 else -1)
    p = head_surface(abs(th), ph)
    if th < 0:
        p = Vector((-p.x, p.y, p.z))
    e = .2
    a = head_surface(min(abs(th) + e, 180), ph) - head_surface(max(abs(th) - e, 0), ph)
    b = head_surface(abs(th), min(ph + e, 89.9)) - head_surface(abs(th), ph - e)
    n = a.cross(b).normalized()
    if n.dot(head_surface(abs(th), ph) - HC) < 0:
        n = -n
    if th < 0:
        n = Vector((-n.x, n.y, n.z))
    return p, n


def build_hair(body_mb):
    """The spiky-01 hairstyle: a cap over the head's scalp (its scalp faces, both sides, raised off the skin) and faceted
    spike clumps on it; a separate object with its own weights (head and the sec-hair chains)."""
    mb = MB()
    rigid = {'head': 1.0}
    scalp = [f for f, p in body_mb.F if p == PID['scalp']]
    used = sorted({i for f in scalp for i in f})
    # vertex normals of the scalp (left half, mirrored on the centre plane) and its hairline (boundary) vertices
    acc = {i: Vector() for i in used}
    edges = {}
    for f in scalp:
        n = face_normal(body_mb, f)
        for i in f:
            acc[i] += n
        for a, b in zip(f, f[1:] + f[:1]):
            edges[frozenset((a, b))] = edges.get(frozenset((a, b)), 0) + 1
    rim = {i for e, c in edges.items() if c == 1 for i in e
           if not all(abs(body_mb.V[j].x) < 1e-7 for j in e)}
    idx = {}
    for side in (1, -1):
        for i in used:
            p = body_mb.V[i]
            if side < 0 and abs(p.x) < 1e-7:
                idx[(i, -1)] = idx[(i, 1)]
                continue
            n = acc[i]
            if abs(p.x) < 1e-7:
                n = Vector((0.0, n.y, n.z))
            n.normalize()
            t = HAIR_CAP[0] if i in rim else _cap_thickness(p)
            q = p + n * t
            idx[(i, side)] = mb.v(Vector((q.x * side, q.y, q.z)), 'hair', rigid)
    for f in scalp:
        mb.f([idx[(i, 1)] for i in f], PID['hair'])
        mb.f([idx[(i, -1)] for i in reversed(f)], PID['hair'])
    rnd = Rand(0x5eed02)

    def on_cap(th, ph, lift):
        p, n = hair_angles_point(th, ph)
        return p + n * (_cap_thickness(p) + lift), n

    clumps = []
    for (r0, r1, lift, W, T, chain) in CLUMPS:
        lift *= 1 + rnd.sym(.08)
        path = []
        for sgm in (0.0, .40, .75, 1.0):
            th = r0[0] + (r1[0] - r0[0]) * sgm
            ph = r0[1] + (r1[1] - r0[1]) * sgm
            path.append(on_cap(th, ph, -.003 if sgm == 0 else lift * sgm ** 1.6 + .0015))
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
    for path, W, T, chain in clumps:
        if chain:
            b1, b2 = bone_of[chain]
            wts = [rigid, {'head': .30, b1: .70}, {b1: .50, b2: .50}, {b2: .80, b1: .20}]
        else:
            wts = [rigid] * 4
        # a leaf: rings of 4 (left edge, ridge, right edge, underside) along the path, then the point
        rings = []
        for k, (sw, sh) in enumerate(((1.0, .55), (.86, 1.0), (.48, .8))):
            c, nn = path[k]
            t = (path[k + 1][0] - c).normalized()
            lat = t.cross(nn).normalized()
            up = lat.cross(t).normalized()
            hw = W * sw * .5
            ring = [c + lat * hw - up * .002, c + up * T * sh, c - lat * hw - up * .002, c - up * (T * sh * .35 + .002)]
            rings.append([mb.v(q, 'hair', wts[k]) for q in ring])
        for k in range(len(rings) - 1):
            mb.strip(rings[k], rings[k + 1], PID['hair'], closed=True)
        tip = mb.v(path[-1][0], 'hair', wts[3])
        last = rings[-1]
        for q in range(4):
            mb.f((last[q], last[(q + 1) % 4], tip), PID['hair'])
        mb.f(list(reversed(rings[0])), PID['hair'])
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
CORE_AT = (.012, 1.285)   # rig x, y on the chest surface: on the sternum between the pecs, over the heart (doc e)
CORE_SCALE = .84          # the shield's size on the leaner chest


def frame_on_surface(tree, x, y):
    hit = tree.ray_cast(Vector((x, y, .6)), Vector((0, 0, -1)))
    n = hit[1] if hit[1].z > 0 else -hit[1]
    up = Vector((0, 1, 0)) - n * n.y
    up.normalize()
    right = up.cross(n)          # character's left (+x) for a front-facing surface
    return hit[0], n, up, right


def poly_area(P):
    return sum(P[i][0] * P[(i + 1) % len(P)][1] - P[(i + 1) % len(P)][0] * P[i][1] for i in range(len(P))) / 2


def ccw(P):
    return list(P) if poly_area(P) >= 0 else list(reversed(P))


def shield(scale_w, scale_h):
    """The core's angular shield outline (local s left, t up), CCW seen from the front: a V notch on top, flat
    shoulders and a pointed base."""
    half = [(0, .030), (.024, .030), (.048, .030), (.058, .016), (.052, -.008), (.034, -.034), (.015, -.052), (0, -.060)]
    full = half + [(-x, y) for x, y in reversed(half[1:-1])]
    return ccw([(x * scale_w * CORE_SCALE, y * scale_h * CORE_SCALE) for x, y in full])


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
DEV_X = J['right-wrist'].x          # -.18
_WY = J['right-wrist'].y            # .847
DEV_Y0, DEV_Y1 = _WY + .018, _WY + .076   # housing span along the forearm
BAND_Y0, BAND_Y1 = _WY + .020, _WY + .070
BAY = dict(y0=_WY + .026, y1=_WY + .062, z0=-.0155, z1=.0155, depth=.0095)
LID_HINGE_Y = _WY + .066
LID_OPEN_DEG = 112.0
CHIP_W, CHIP_H, CHIP_T = .026, .031, .0040
CHIP_APPROACH = (Vector((-.040, .012, .006)), 28.0)   # chip offset (rig) and tilt (deg) before it seats


def forearm_radius(y, phi):
    """Right forearm skin radius at height y, angle phi (0 = outward -x, 90 = front +z): the arm table, mirrored."""
    return limb_radius(ARM, y, phi)


# The housing's lateral offsets were designed on a .034 m forearm; DEV_IN moves them in onto this slimmer one.
DEV_IN = .034 - forearm_radius((DEV_Y0 + DEV_Y1) / 2, 0.0)
DEV_XO = DEV_X - .0605 + DEV_IN     # the housing's outer face


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
    xb = DEV_X - .040 + DEV_IN # embedded in the band
    xo = DEV_XO                # outer face
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
    scr = [Vector((DEV_X - .050 + DEV_IN, _WY + .024, .0352)), Vector((DEV_X - .050 + DEV_IN, _WY + .070, .0352)),
           Vector((DEV_X - .041 + DEV_IN, _WY + .070, .0425)), Vector((DEV_X - .041 + DEV_IN, _WY + .024, .0425))]
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
PART_CODES = ['torso', 'head', 'arm', 'hand', 'leg', 'foot', 'face', 'ear', 'finger', 'hair', 'piece', 'neck', 'trim']


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.unit_settings.system = 'METRIC'
    sc.unit_settings.scale_length = 1.0
    sc.render.fps = 30
    return sc


# Shading: big planes shade smooth inside and break crisply at their edges. Edges are sharp where the faces meet at more
# than SHARP_DEG, at paint borders, along the design creases and on flat pieces; features on the face stay flat.
SHARP_DEG = {'body': 32.0, 'hair': 34.0, 'piece': 0.0}
HEAD_SHARP_DEG = 30.0         # the head's smaller planes: only the design creases and paint borders break them
FOOT_SHARP_DEG = 21.0         # the feet's tight folds (heel, ankle front, toe block) stay crisp
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
            t = thr
            if kind == 'body' and all(mb.R[i] == 'head' for i in e):
                t = math.radians(HEAD_SHARP_DEG)
            elif kind == 'body' and any(mb.R[i] == 'foot' for i in e):
                t = math.radians(FOOT_SHARP_DEG)
            if normals[f0].angle(normals[f1], 0) > t or mb.F[f0][1] != mb.F[f1][1]:
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
    'motion-root': (0, ROOT_HEIGHT + .12, 0), 'pelvis': (0, J['pelvis'].y - .14, 0), 'spine': J['chest'],
    'chest': J['neck'], 'neck': J['head'], 'head': (0, HC.y + .10, 0),
    'left-clavicle': J['left-shoulder'], 'left-shoulder': J['left-elbow'], 'left-elbow': J['left-wrist'],
    'left-wrist': (J['left-wrist'].x, J['left-wrist'].y - .13, 0), 'left-hip': J['left-knee'], 'left-knee': J['left-ankle'],
    'left-ankle': J['left-toe'], 'left-toe': (J['left-toe'].x, .03, .20),
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
    hinge = Vector((DEV_XO - .0032, LID_HINGE_Y, 0))
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
    """The cleaned weights for one body vertex: rig-space position p, its part tag and its automatic weights. Limb
    blends are placed relative to the visual joints."""
    x, y, z = p
    s = 'left-' if x >= 0 else 'right-'
    w_y, e_y, s_y = J['left-wrist'].y, J['left-elbow'].y, J['left-shoulder'].y
    a_y, k_y, h_y = J['left-ankle'].y, J['left-knee'].y, J['left-hip'].y
    if part in ('face', 'ear', 'head'):
        return {'head': 1.0}
    if part == 'neck':
        return {'head': .5, 'neck': .5}
    if part in ('hand', 'finger'):
        return {s + 'wrist': 1.0}
    if part == 'arm':
        if y <= ARM[ARM_CUFF_ROW][0] + .001:
            return {s + 'wrist': 1.0}
        if y < e_y - .03:
            t = smooth(w_y + .08, w_y + .18, y)
            return {s + 'wrist': 1 - t, s + 'elbow': t}
        if y < e_y + .036:
            t = smooth(e_y - .03, e_y + .036, y)
            return {s + 'elbow': 1 - t, s + 'shoulder': t}
        if y < s_y - .10:
            return {s + 'shoulder': 1.0}
    if part == 'leg':
        if y < a_y + .08:
            t = smooth(a_y + .015, a_y + .08, y)
            return {s + 'ankle': 1 - t, s + 'knee': t}
        if y < k_y - .03:
            return {s + 'knee': 1.0}
        if y < k_y + .032:
            t = smooth(k_y - .03, k_y + .032, y)
            return {s + 'knee': 1 - t, s + 'hip': t}
        if y < h_y - .14:
            return {s + 'hip': 1.0}
    if part == 'foot':
        t = smooth(.100, .140, z)
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


HEAT_ARM_SPREAD = 40.0       # degrees: the arms are spread into an A-pose for the bone-heat pass only


def auto_weights(body, rig):
    """Bone-heat weights on a copy of the body's main closed shell (overlays, ears and fingers left out). The arms hang
    against the lats at rest, so the pass runs on an A-posed copy (the arm vertices and the arm bones turned out about
    the shoulder joints by HEAT_ARM_SPREAD); the weights carry back unchanged, vertex for vertex."""
    keep = {PART_CODES.index(p) for p in ('torso', 'head', 'arm', 'hand', 'leg', 'foot', 'neck')}
    arm_parts = {PART_CODES.index(p) for p in ('arm', 'hand')}
    tmp = body.copy()
    tmp.data = body.data.copy()
    tmp.name = 'CA_HeatTmp'
    link(tmp)
    parts = [d.value for d in tmp.data.attributes['ca_part'].data]
    spread = {}
    for side, sign in (('left-', 1), ('right-', -1)):
        spread[sign] = (Matrix.Translation(B(J[side + 'shoulder'])) @
                        Matrix.Rotation(math.radians(HEAT_ARM_SPREAD) * sign, 4, Vector((0, -1, 0))) @
                        Matrix.Translation(-B(J[side + 'shoulder'])))
    for v in tmp.data.vertices:
        if parts[v.index] in arm_parts:
            v.co = spread[1 if v.co.x >= 0 else -1] @ v.co
    bm = bmesh.new()
    bm.from_mesh(tmp.data)
    vid = bm.verts.layers.int.new('ca_vid')
    for v in bm.verts:
        v[vid] = v.index
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if parts[v.index] not in keep], context='VERTS')
    bm.to_mesh(tmp.data)
    bm.free()
    trig = rig.copy()
    trig.data = rig.data.copy()
    trig.name = 'CA_HeatRig'
    link(trig)
    view = bpy.context.view_layer
    view.objects.active = trig
    bpy.ops.object.mode_set(mode='EDIT')
    for side, sign in (('left-', 1), ('right-', -1)):
        for n in ('shoulder', 'elbow', 'wrist'):
            eb = trig.data.edit_bones[side + n]
            eb.head = spread[sign] @ eb.head
            eb.tail = spread[sign] @ eb.tail
    bpy.ops.object.mode_set(mode='OBJECT')
    for o in view.objects:
        o.select_set(False)
    tmp.select_set(True)
    trig.select_set(True)
    view.objects.active = trig
    bpy.ops.object.parent_set(type='ARMATURE_AUTO')
    ids = [d.value for d in tmp.data.attributes['ca_vid'].data]
    groups = {g.index: g.name for g in tmp.vertex_groups}
    out = {}
    for v in tmp.data.vertices:
        out[ids[v.index]] = {groups[g.group]: g.weight for g in v.groups if g.weight > 0}
    bpy.data.objects.remove(tmp)
    bpy.data.objects.remove(trig)
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
        front = z > HC.z - .025 and y < HC.y + .30 * HEAD_H and part != 'ear'
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
    weights = [None if part == 'trim' else limit_normalize(weights_rule(part, rig_pos(v.co), auto.get(v.index, {})))
               for v, part in zip(body.data.vertices, parts)]
    skin = [i for i, part in enumerate(parts) if part in ('torso', 'leg')]
    for i, part in enumerate(parts):
        if part == 'trim':      # the piping takes the weights of the nearest cloth vertex under it
            co = body.data.vertices[i].co
            j = min(skin, key=lambda k: ((body.data.vertices[k].co - co).length_squared, k))
            weights[i] = weights[j]
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
