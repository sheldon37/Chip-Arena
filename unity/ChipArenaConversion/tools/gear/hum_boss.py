"""THE HUM, Level 0's boss, modelled in Blender (docs/EXPEDITION-BOSS-BACKROOMS.md section 3). It writes the local bank
Resources/ChipArenaPackBossHum (gearkit's format, tools/pack_bank/bank.py): per tier a smooth skinned body, its hardware skinned rigidly
on the same bones, and the lit parts (the head's tubes and diffuser, the seven spine tubes, the Ballast) as their own rigid props so
each takes its own light. Runtime/NativeHumBossBody.cs draws it on Core HumBossRig's pose.

usage: blender --background --factory-startup --python tools/gear/hum_boss.py -- --out ASSETS/LocalAssets [--tiers 0,1,2] [--sheet DIR] [--table]

The design: what has been wandering nearby. It is the Lobby's fluorescent circuit grown a body out of wet carpet and wallpaper: very tall
and very thin, stooped under a 4 m ceiling, standing on long arched hands, with one recessed ceiling fixture where a face should be. It is
an original design; no creature from any film, game or web series.

Axes: the boss's own (+x its right, +y up, +z the way it faces; `U` takes them to Blender's). The body is modelled in its bind pose, the
neutral stoop, and every mesh is stored in the model space of that pose. A joint's number is its row in JOINTS; Core NativeHumBossLook holds
the same table (`--table` prints it; Tests/HumBossChecks holds the two to each other). Skin weights go in the bank's uv channels: uv0 is
(bone0 + weight0 x .998, bone1 + weight1 x .998), uv1 the same for bones 2 and 3. Nothing here is read by a rule.
"""
import argparse
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector, noise
from mathutils.bvhtree import BVHTree

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gearkit as gk  # noqa: E402
from gearkit import Piece, finish  # noqa: E402

BANK = 'ChipArenaPackBossHum'
# The design frame (Blender: X right, Y forward, Z up) to the model's (Unity's axes: x right, y up, z forward).
TO_MODEL = Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
BUDGET = (40000, 20000, 8000)


def U(x, y=None, z=None):
    """The boss's axes to Blender's."""
    if y is None:
        x, y, z = x
    return Vector((x, z, y))


def V(x, y, z):
    return Vector((x, y, z))


# ===========================================================================================================================
# The skeleton in the bind pose (boss axes). The order is Core NativeHumBossLook's.
# ===========================================================================================================================
JOINTS, INDEX = [], {}


def J(name, parent, x, y, z):
    INDEX[name] = len(JOINTS)
    JOINTS.append((name, parent, V(x, y, z)))


def at(name):
    return JOINTS[INDEX[name]][2]


UPPER_ARM, FORE_ARM, PALM = 1.62, 1.62, .16
THIGH, SHIN = 1.0, 1.02
WRIST = (.92, .40, .62)
ANKLE = (.34, .42, -.86)
HIP = (.26, 2.02, -.62)
SHOULDER = (.42, 3.30, .15)
ARM_POLE = (.85, .35, -.40)
LEG_POLE = (.03, 0, 1)
FINGER_LINKS = ((.40, .32, .27), (.45, .36, .30), (.43, .35, .31), (.36, .29, .25))
FINGER_YAW = (-34, -12, 8, 31)  # outer side positive on either hand after mirroring
FINGER_ROOT_X = (-.115, -.04, .04, .115)
FINGER_PITCH = ((8, -36, -72), (12, -38, -70), (10, -40, -72), (6, -32, -68))
STRIP_LINKS, STRIPS, WIRE_LINKS, TUBES, TILES = 4, 10, 2, 7, 4
WIRE_LENGTH = .24
# Where each wallpaper strip hangs from, its parent, length and width (boss axes).
STRIP_DEFS = (
    ('clav-l', -.46, 3.44, .02, 1.82, .21), ('clav-r', .46, 3.44, .02, 1.56, .17),
    ('chest', -.26, 3.54, -.30, 1.94, .22), ('chest', .24, 3.52, -.28, 1.46, .18),
    ('spine1', -.27, 3.04, -.50, 1.40, .17), ('spine1', .29, 2.98, -.46, 1.72, .20),
    ('pelvis', -.40, 2.20, -.52, 1.50, .19), ('pelvis', .40, 2.17, -.54, 1.34, .17),
    ('pelvis', -.20, 2.16, -.84, 1.62, .20), ('pelvis', .24, 2.13, -.84, 1.20, .15))
# Each tube's centre on the back, the way it runs (a unit vector down the spine, turned out to alternate sides: a herringbone), and its parent.
TUBE_LENGTH = .62
TILE_DEFS = (('rib-l', -.42, 2.96, -.12), ('rib-l', -.40, 3.24, .06), ('rib-r', .41, 2.90, -.06), ('rib-r', .36, 2.72, .10))


def two_bone(root, target, l1, l2, pole):
    d = target - root
    c = d.length
    dr = d / c
    a = (l1 * l1 - l2 * l2 + c * c) / (2 * c)
    h = math.sqrt(max(0, l1 * l1 - a * a))
    p = pole - dr * pole.dot(dr)
    p.normalize()
    return root + dr * a + p * h


def spine_point(u):
    """A point on the back's ridge, u 0 (the sacrum) to 1 (the neck's root), with its outward normal."""
    pts = [V(0, 2.14, -.84), V(0, 2.52, -.89), V(0, 2.90, -.80), V(0, 3.24, -.57), V(0, 3.54, -.28), V(0, 3.70, .06), V(0, 3.62, .34)]
    n = len(pts) - 1
    x = max(0.0, min(.9999, u)) * n
    i = int(x)
    p = pts[i].lerp(pts[i + 1], x - i)
    d = (pts[min(n, i + 1)] - pts[i]).normalized()
    return p, d


def tube_frame(i):
    """Tube i (0 at the hips): its centre, the unit vector it runs along (herringbone), and the back's normal there."""
    u = .08 + i * .135
    p, d = spine_point(u)
    out = V(0, d.z, -d.y).normalized()
    if out.dot(V(0, .6, -1)) < 0:
        out = -out
    side = -1 if i % 2 == 0 else 1
    run = (d + V(side * .42, 0, 0)).normalized()
    return p + out * .06, run, out


def tube_parent(i):
    return ('pelvis', 'spine0', 'spine0', 'spine1', 'spine1', 'chest', 'chest')[i]


def build_joints():
    J('root', None, 0, 0, 0)
    J('pelvis', 'root', 0, 2.05, -.62)
    J('spine0', 'pelvis', 0, 2.62, -.62)
    J('spine1', 'spine0', 0, 3.14, -.44)
    J('chest', 'spine1', 0, 3.50, -.06)
    J('neck0', 'chest', 0, 3.50, .30)
    J('neck1', 'neck0', 0, 3.42, .62)
    J('neck2', 'neck1', 0, 3.35, .95)
    J('head', 'neck2', 0, 3.30, 1.24)
    J('jaw-u', 'head', 0, 3.49, 1.38)
    J('jaw-l', 'head', 0, 3.11, 1.38)
    for s, n in ((-1, 'l'), (1, 'r')):
        J('clav-' + n, 'chest', s * .15, 3.47, .08)
        sh = V(s * SHOULDER[0], SHOULDER[1], SHOULDER[2])
        wr = V(s * WRIST[0], WRIST[1], WRIST[2])
        el = two_bone(sh, wr, UPPER_ARM, FORE_ARM, V(s * ARM_POLE[0], ARM_POLE[1], ARM_POLE[2]))
        J('upper-' + n, 'clav-' + n, *sh)
        J('fore-' + n, 'upper-' + n, *el)
        J('hand-' + n, 'fore-' + n, *wr)
    for s, n in ((-1, 'l'), (1, 'r')):
        hp = V(s * HIP[0], HIP[1], HIP[2])
        an = V(s * ANKLE[0], ANKLE[1], ANKLE[2])
        kn = two_bone(hp, an, THIGH, SHIN, V(s * LEG_POLE[0], LEG_POLE[1], LEG_POLE[2]))
        J('thigh-' + n, 'pelvis', *hp)
        J('shin-' + n, 'thigh-' + n, *kn)
        J('foot-' + n, 'shin-' + n, *an)
    J('rib-l', 'chest', -.10, 3.42, -.30)
    J('rib-r', 'chest', .10, 3.42, -.30)
    J('ballast', 'chest', 0, 3.04, -.12)
    # The fingers: each hand's four, three links each, in the planted arch.
    for h, (s, n) in enumerate(((-1, 'l'), (1, 'r'))):
        wr = V(s * WRIST[0], WRIST[1], WRIST[2])
        for f in range(4):
            yaw = math.radians(FINGER_YAW[f] * s)
            p = wr + V(s * FINGER_ROOT_X[f], .06, .20 - abs(FINGER_ROOT_X[f]) * .25)
            parent = 'hand-' + n
            for k in range(3):
                name = 'finger-%s%d-%d' % (n, f, k)
                J(name, parent, *p)
                pitch = math.radians(FINGER_PITCH[f][k])
                bend = yaw + math.radians((k - 1) * FINGER_YAW[f] * s * .22)
                p = p + V(math.sin(bend) * math.cos(pitch), math.sin(pitch), math.cos(bend) * math.cos(pitch)) * FINGER_LINKS[f][k]
                parent = name
    for i in range(TUBES):
        c, run, out = tube_frame(i)
        J('tube-%d' % i, tube_parent(i), *c)
    for i in range(TUBES):
        c, run, out = tube_frame(i)
        p = c - run * (TUBE_LENGTH * .5 + .03)
        for k in range(WIRE_LINKS):
            J('wire-%d-%d' % (i, k), 'tube-%d' % i if k == 0 else 'wire-%d-%d' % (i, k - 1), *(p + V(0, -WIRE_LENGTH * k, 0)))
    for s, (parent, x, y, z, length, width) in enumerate(STRIP_DEFS):
        for k in range(STRIP_LINKS):
            J('strip-%d-%d' % (s, k), parent if k == 0 else 'strip-%d-%d' % (s, k - 1), x, y - length * k / STRIP_LINKS, z)
    for i, (parent, x, y, z) in enumerate(TILE_DEFS):
        J('tile-%d' % i, parent, x, y, z)
    # The arm that comes out of the wall (Wall Hand): a second forearm, hand and fingers, bound where the right arm's are; hidden until it's used.
    J('wall-fore', 'root', *at('fore-r'))
    J('wall-hand', 'wall-fore', *at('hand-r'))
    for f in range(4):
        for k in range(3):
            J('wall-finger-%d-%d' % (f, k), 'wall-hand' if k == 0 else 'wall-finger-%d-%d' % (f, k - 1), *at('finger-r%d-%d' % (f, k)))


build_joints()


def segment_of(name):
    """A joint's bone as a segment (its start, its end), for the skin weights."""
    a = at(name)
    nexts = {'pelvis': 'spine0', 'spine0': 'spine1', 'spine1': 'chest', 'chest': 'neck0', 'neck0': 'neck1', 'neck1': 'neck2', 'neck2': 'head'}
    if name in nexts:
        return a, at(nexts[name])
    if name == 'head':
        return a + V(0, 0, -.17), a + V(0, 0, -.12)
    if name == 'wall-fore':
        return a, at('wall-hand')
    if name == 'wall-hand':
        return a, a + V(0, .06, .22)
    for n in ('l', 'r'):
        if name == 'clav-' + n:
            return a, at('upper-' + n)
        if name == 'upper-' + n:
            return a, at('fore-' + n)
        if name == 'fore-' + n:
            return a, at('hand-' + n)
        if name == 'thigh-' + n:
            return a, at('shin-' + n)
        if name == 'shin-' + n:
            return a, at('foot-' + n)
        if name == 'hand-' + n:
            return a, a + V(0, .06, .22)
        if name == 'foot-' + n:
            return a, a + V(0, -.34, .44)
    return a, a


HIDE_BONES = ['pelvis', 'spine0', 'spine1', 'chest', 'neck0', 'neck1', 'neck2', 'head', 'clav-l', 'upper-l', 'fore-l', 'hand-l', 'clav-r', 'upper-r', 'fore-r', 'hand-r',
              'thigh-l', 'shin-l', 'foot-l', 'thigh-r', 'shin-r', 'foot-r']


# ===========================================================================================================================
# Finishes
# ===========================================================================================================================
def make_finishes():
    finish('hide', 0x2B211A, 'hide')
    finish('slab', 0x33281F, 'hide')
    finish('backing', 0x6A5C43, 'cloth')
    finish('paper', 0x8E9246, 'paper', outline=True)
    finish('plaster', 0x8F8A70, 'plaster', outline=True)
    finish('rail', 0xB9B5A6, 'paint', under=0x5D4A38)
    finish('rail-dark', 0x7B7768, 'paint', under=0x46372A)
    finish('tile', 0xB5AE96, 'tile')
    finish('board', 0x5C2E1B, 'paint', under=0x2A150C)
    finish('board-dark', 0x3F2013, 'paint', under=0x1C0E08)
    finish('enamel', 0xCFCBB9, 'paint', under=0x6E5F4C, dirt=1.1)
    finish('enamel-dark', 0x4D4A42, 'paint', under=0x2E2A24)
    finish('steel', 0x6E6E68, 'metal')
    finish('ballast-case', 0x1E1D1B, 'metal')
    finish('core', 0x34332F, 'metal')
    finish('copper', 0xB9672A, 'metal')
    finish('socket', 0xD8D4C4, 'plastic')
    finish('alu', 0xA8A8A0, 'metal')
    finish('wire', 0x241F1B, 'rubber')
    finish('wire-red', 0x4A1712, 'rubber')
    finish('wire-white', 0x8D8A7E, 'rubber')
    finish('conduit', 0x4F4E48, 'metal')
    finish('glass', 0xE8E6D8, 'glassy', outline=False)
    finish('glow-tube', 0xFFFBEE, 'glow')
    finish('glow-diffuser', 0xFFF9E4, 'glow')
    finish('glow-amber', 0xFF9A2A, 'glow')
    finish('glow-coil', 0xFFB048, 'glow')


def _smooth(e0, e1, x):
    t = max(0.0, min(1.0, (x - e0) / (e1 - e0)))
    return t * t * (3 - 2 * t)


def clamp3(c):
    return Vector((max(0.0, min(1.0, c.x)), max(0.0, min(1.0, c.y)), max(0.0, min(1.0, c.z))))


def chevron(u, v):
    """The level's paper: u across the strip (m), v along it (m). Stripes every 12 cm, arcs across them."""
    s = abs(((u / .12) % 1.0) - .5) * 2
    a = ((v + abs(((u / .12) % 1.0) - .5) * .05) / .045) % 1.0
    return .78 if s > .86 else (.82 if a < .3 else 1.0), s > .86


def shade(f, p, n, cav, ao, seed, extra=None):
    """A vertex's colour (sRGB 0..1): gearkit's finishes, the hide, the paper and the tile, darkened by the ambient occlusion."""
    q = Vector(p) + Vector((seed * 3.1, seed * 1.7, seed * 2.3))
    if f.glow:
        c = Vector(f.colour)
        if extra is not None:
            c = c * extra
        return clamp3(c)
    if f.kind == 'hide':
        # Soaked carpet pile: matted fibres, the sodden patches blacker and shining where they face up, worn tips a little paler,
        # creases near black.
        fibres = noise.noise(q * 58) * .6 + noise.noise(q * 131) * .4
        mats = noise.noise(q * 13)
        sodden = _smooth(-.05, .32, noise.noise(q * 3.3 + Vector((7, 2, 1))))
        c = Vector(f.colour) * (.82 + .55 * fibres + .28 * mats)
        c = c.lerp(Vector((.045, .04, .042)), sodden * .82)
        sheen = _smooth(.2, .8, n.z) * sodden * _smooth(.0, .5, noise.noise(q * 23))
        c = c + Vector((.05, .055, .065)) * sheen
        c = c * (1 - .55 * _smooth(.02, .3, cav))
        return clamp3(c * (.3 + .7 * ao))
    if f.kind == 'paper':
        tone, rib = (extra if extra is not None else (1.0, False))
        c = Vector(f.colour) * tone
        if rib:
            c = c + Vector((.04, .04, .02))
        stain = _smooth(.1, .55, noise.noise(q * 4.5)) * .35
        c = c.lerp(Vector((.27, .25, .14)), stain)
        c = c * (1 - .5 * _smooth(.02, .3, cav))
        return clamp3(c * (.45 + .55 * ao))
    if f.kind == 'plaster':
        c = Vector(f.colour) * (.9 + .25 * noise.noise(q * 37))
        c = c.lerp(Vector((.42, .36, .25)), .45 * _smooth(.0, .5, noise.noise(q * 6)))
        return clamp3(c * (.45 + .55 * ao))
    if f.kind == 'tile':
        # Mineral fibre board: pinholes, a sagged water stain ring, grime.
        c = Vector(f.colour) * (.92 + .16 * noise.noise(q * 60))
        pin = noise.noise(q * 150)
        if pin > .34:
            c = c * .55
        ring = noise.noise(q * 5.5)
        c = c.lerp(Vector((.5, .38, .2)), .55 * _smooth(.02, .12, ring) * (1 - _smooth(.2, .3, ring)))
        c = c.lerp(Vector((.32, .26, .16)), .5 * _smooth(.28, .5, ring))
        return clamp3(c * (.42 + .58 * ao))
    if f.kind == 'glassy':
        return clamp3(Vector(f.colour) * (.5 + .5 * ao))
    return clamp3(gk.shade(f, p, n, cav, seed) * (.34 + .66 * ao))


# ===========================================================================================================================
# Building pieces
# ===========================================================================================================================
class Part:
    """An object with how it is skinned: `rigid` one joint, else the nearest of `bones`."""
    __slots__ = ('ob', 'rigid', 'bones', 'ratio', 'minimum', 'colour')

    def __init__(self, ob, rigid=None, bones=None, ratio=1.0, minimum=70, colour=None):
        self.ob, self.rigid, self.bones, self.ratio, self.minimum, self.colour = ob, rigid, bones, ratio, minimum, colour


def make(pc, rigid=None, bones=None, ratio=1.0, minimum=70, colour=None):
    return Part(pc.make(), rigid, bones, ratio, minimum, colour)


def path(*pts):
    return [U(p) for p in pts]


def rope(pc, pts, radii, fin='hide', per=5, segs=8):
    """A tube through the points with radii at each: the body's sinews (Catmull-Rom, radii eased between)."""
    P = gk.catmull(pts, per=per)
    n = len(pts) - 1
    R = []
    for i in range(len(P)):
        x = i / per
        k = min(n - 1, int(x))
        a = x - k
        R.append(radii[k] + (radii[k + 1] - radii[k]) * (a * a * (3 - 2 * a)))
    return gk.tube(pc, P, 0, fin, segs=segs, radii=R)


def knob(pc, centre, radii, fin='hide', along=None):
    """An ellipsoid; `along` turns its z axis onto that direction."""
    r = bmesh.ops.create_icosphere(pc.bm, subdivisions=3, radius=1.0)
    rot = Vector((0, 0, 1)).rotation_difference(along.normalized()).to_matrix().to_4x4() if along is not None else Matrix()
    gk._place(pc.bm, r['verts'], Matrix.Translation(centre) @ rot @ Matrix.Diagonal((*radii, 1)))
    return pc.tag(gk._faces_of(r['verts']), fin)


def jitter(seed, k, amp):
    rng = random.Random(seed * 7919 + k)
    return Vector((rng.uniform(-amp, amp), rng.uniform(-amp, amp), rng.uniform(-amp, amp)))


# ---------------------------------------------------------------------------------------------------------------------------
# The hide: ropes of wet carpet joined by a voxel remesh into one skin, its pile and sags pushed in as displacement
# ---------------------------------------------------------------------------------------------------------------------------
def hide_ropes(pc):
    # The spine and the long back muscles (the front of the torso is open: the ribs are hardware).
    rope(pc, path((0, 1.98, -.70), (0, 2.28, -.76), (0, 2.66, -.76), (0, 3.02, -.62), (0, 3.34, -.34), (0, 3.56, -.02), (0, 3.54, .24)), (.10, .11, .12, .14, .16, .16, .11), segs=12)
    for s in (-1, 1):
        rope(pc, path((s * .10, 2.24, -.72), (s * .15, 2.70, -.72), (s * .20, 3.08, -.58), (s * .26, 3.38, -.28), (s * .34, 3.48, .02), (s * .42, 3.42, .12)), (.05, .08, .10, .11, .10, .08))
        rope(pc, path((s * .06, 2.20, -.84), (s * .07, 2.68, -.86), (s * .08, 3.06, -.70), (s * .09, 3.40, -.40)), (.05, .06, .06, .05))
        # Flank sinews down to the hip, the ribs' anchors.
        rope(pc, path((s * .30, 3.28, -.30), (s * .27, 2.80, -.46), (s * .28, 2.40, -.54), (s * .30, 2.14, -.54)), (.035, .03, .03, .05))
        rope(pc, path((s * .18, 3.20, -.46), (s * .17, 2.70, -.60), (s * .22, 2.28, -.60)), (.04, .03, .05))
    # The knobs of the spine under the tubes.
    for i in range(13):
        u = .02 + i * .079
        p, d = spine_point(u)
        out = V(0, d.z, -d.y).normalized()
        if out.dot(V(0, .6, -1)) < 0:
            out = -out
        knob(pc, U(p - out * .04), (.055, .055, .06), along=U(out))
    # The pelvis: a narrow bar, the crests, the sacrum.
    rope(pc, path((-.30, 2.03, -.60), (0, 2.08, -.66), (.30, 2.03, -.60)), (.10, .12, .10), segs=10)
    knob(pc, U(0, 2.02, -.78), (.12, .10, .14))
    for s in (-1, 1):
        knob(pc, U(s * .30, 2.20, -.54), (.075, .05, .11), along=U(.2 * s, .9, .3))
    # The neck: long, thin, with tendons standing off it.
    rope(pc, path((0, 3.52, .20), (0, 3.50, .42), (0, 3.45, .68), (0, 3.38, .94), (0, 3.33, 1.10)), (.10, .082, .068, .06, .07), segs=10)
    for s in (-1, 1):
        rope(pc, path((s * .07, 3.44, .10), (s * .065, 3.42, .42), (s * .05, 3.37, .74), (s * .045, 3.32, 1.06)), (.03, .024, .022, .026), segs=6)
    # The arms.
    for s, n in ((-1, 'l'), (1, 'r')):
        sh, el, wr = at('upper-' + n), at('fore-' + n), at('hand-' + n)
        a = (el - sh).normalized()
        b = (wr - el).normalized()
        side = a.cross(V(0, 0, 1)).normalized()
        out = (el - (sh + wr) * .5).normalized()
        cl = at('clav-' + n)
        rope(pc, path(cl, sh * .5 + cl * .5 + V(0, .05, 0), sh + V(0, .05, 0), sh + a * .3), (.09, .12, .135, .09), segs=10)
        rope(pc, path(sh + a * .05, sh + a * .55 + out * .02, sh + a * 1.1, el - a * .04), (.09, .082, .058, .05))
        rope(pc, path(sh + a * .1 + side * .05 - out * .05, sh + a * .62 + side * .07, sh + a * 1.25 + side * .03, el - a * .05), (.05, .055, .03, .03), segs=6)
        rope(pc, path(sh + a * .22 - side * .06, sh + a * .85 - side * .06 + out * .02, el - a * .08 - side * .015), (.04, .045, .025), segs=6)
        # The elbow, its point standing out behind.
        knob(pc, U(el), (.062, .062, .085), along=U(a + b))
        # The forearm: two strands winding round each other, thinning to a bony wrist.
        for k in (0, 1):
            ph = k * math.pi
            pts, rad = [], []
            for i in range(7):
                t = i / 6
                ang = ph + t * 3.2
                off = (side * math.cos(ang) + out * math.sin(ang)) * (.042 - .018 * t)
                pts.append(U(el + (wr - el) * (.02 + t * .96) + off))
                rad.append(max(.03, (.062 if t < .35 else .055) * (1 - .5 * t) * (1.1 if k == 0 else .95)))
            rope(pc, pts, rad, per=3, segs=6)
        knob(pc, U(wr), (.052, .042, .06))
        # The palm: a bony fan of the fingers' roots.
        for f in range(4):
            k0 = at('finger-%s%d-0' % (n, f))
            rope(pc, path(wr + V(0, -.005, 0), wr * .45 + k0 * .55 + V(0, .018, 0), k0 + V(0, .0, .015)), (.034, .03, .036), per=3, segs=6)
        knob(pc, U(wr + V(0, .03, .11)), (.11, .03, .10))
    # The legs: long thigh, high knob of a knee, calf high and thin, heel raised on a long foot.
    for s, n in ((-1, 'l'), (1, 'r')):
        hp, kn, an = at('thigh-' + n), at('shin-' + n), at('foot-' + n)
        a = (kn - hp).normalized()
        b = (an - kn).normalized()
        side = a.cross(V(1, 0, 0)).normalized()
        rope(pc, path(hp + V(0, .06, .02), hp + a * .3, hp + a * .68, kn - a * .04), (.105, .115, .08, .06), segs=10)
        rope(pc, path(hp + V(s * .05, -.03, -.06), hp + a * .42 + V(s * .05, 0, -.075), kn - a * .1 + V(s * .01, 0, -.05)), (.06, .065, .028), segs=6)
        rope(pc, path(hp + V(-s * .05, -.05, .0), hp + a * .5 + V(-s * .055, 0, .02), kn - a * .05 + V(-s * .03, 0, 0)), (.05, .05, .025), segs=6)
        knob(pc, U(kn + V(0, 0, .018)), (.062, .075, .06))
        rope(pc, path(kn + b * .02, kn + b * .38, an - b * .08), (.062, .052, .038), segs=8)
        rope(pc, path(kn + b * .05 - V(0, 0, .055), kn + b * .3 + V(0, 0, -.075), kn + b * .7 + V(0, 0, -.018)), (.04, .058, .02), segs=6)
        knob(pc, U(an), (.042, .045, .05))
        # The long foot (the toes and spurs are built apart: toe_parts).
        ball = an + V(s * .01, -.34, .36)
        rope(pc, path(an + V(0, -.02, .02), an * .5 + ball * .5, ball), (.048, .042, .05), segs=7)


def toe_parts(tier):
    """The toes, the heels' and elbows' spurs: smooth tapering hide outside the voxel skin, rigid on the foot and the forearm."""
    out = []
    for s, n in ((-1, 'l'), (1, 'r')):
        pc = Piece('toes-' + n)
        an = at('foot-' + n)
        ball = an + V(s * .01, -.34, .36)
        seg = 8 if tier == 0 else 5
        rope(pc, path(an + V(0, .01, -.01), an + V(0, .05, -.10), an + V(0, .05, -.18)), (.04, .026, .006), segs=seg)
        for t, (dx, dz) in enumerate(((-.095, .27), (.0, .33), (.095, .26))):
            tip = ball + V(dx * s, -.075, dz)
            rope(pc, path(ball + V(dx * .25 * s, 0, .0), ball + V(dx * .75 * s, .025, dz * .55), tip), (.034, .026, .007), per=3, segs=seg)
        out.append(make(pc, rigid='foot-' + n, ratio=1, minimum=30))
        pc = Piece('spur-' + n)
        sh, el, wr = at('upper-' + n), at('fore-' + n), at('hand-' + n)
        o = (el - (sh + wr) * .5).normalized()
        b = (wr - el).normalized()
        rope(pc, path(el - o * .02, el + o * .1 - b * .02, el + o * .2 - b * .05), (.05, .03, .006), segs=seg)
        out.append(make(pc, rigid='fore-' + n, ratio=1, minimum=20))
    return out


def build_hide(tier):
    pc = Piece('hide')
    hide_ropes(pc)
    ob = pc.make()
    fine = ob.modifiers.new('voxel', 'REMESH')
    fine.mode = 'VOXEL'
    fine.voxel_size = .016
    fine.use_smooth_shade = True
    sm = ob.modifiers.new('smooth', 'SMOOTH')
    sm.factor = .6
    sm.iterations = 8
    deps = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(ob.evaluated_get(deps))
    ob.modifiers.clear()
    old = ob.data
    ob.data = me
    bpy.data.meshes.remove(old)
    # The pile: mats, sags, a tufted surface (the pile is denser on the back and shoulders, finer down the limbs).
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.normal_update()
    for v in bm.verts:
        p = v.co
        big = noise.noise(p * 4.5 + Vector((3, 1, 7)))
        mat = noise.noise(p * 15) + .5 * noise.noise(p * 27 + Vector((9, 4, 2)))
        tuft = noise.noise(p * 62)
        limb = _smooth(1.0, 2.6, p.z) * .5 + .5
        v.co += v.normal * (.012 * big + (.010 * mat + .0055 * tuft) * limb)
    bm.to_mesh(me)
    bm.free()
    # To the budget: one even skin (quads), so it deforms evenly and the colour bake has the same density everywhere.
    rm = ob.modifiers.new('voxel', 'REMESH')
    rm.mode = 'VOXEL'
    rm.voxel_size = .024
    rm.use_smooth_shade = True
    sm2 = ob.modifiers.new('smooth', 'SMOOTH')
    sm2.factor = .3
    sm2.iterations = 1
    deps = bpy.context.evaluated_depsgraph_get()
    faces = sum(1 for _ in ob.evaluated_get(deps).to_mesh().loop_triangles) if False else len(ob.evaluated_get(deps).to_mesh().polygons)
    target = {0: 6500, 1: 4200, 2: 2200}[tier]
    dec = ob.modifiers.new('dec', 'DECIMATE')
    dec.ratio = min(1.0, target / faces)
    dec.use_collapse_triangulate = True
    return Part(ob, bones=HIDE_BONES)


# ---------------------------------------------------------------------------------------------------------------------------
# Carpet slabs and tatters: the wet carpet it has dragged on
# ---------------------------------------------------------------------------------------------------------------------------
def slab(pc, pts, widths, thick, seed, segs_u=6):
    """A sagging slab of carpet along the points (boss axes), `widths` across, its underside the jute backing, its long edges torn."""
    P = gk.catmull(pts, per=4)
    n = len(P)
    rng = random.Random(seed)
    rows = []
    T = [(P[min(i + 1, n - 1)] - P[max(i - 1, 0)]).normalized() for i in range(n)]
    for i, p in enumerate(P):
        x = i / (n - 1) * (len(widths) - 1)
        k = min(len(widths) - 2, int(x))
        w = widths[k] + (widths[k + 1] - widths[k]) * (x - k)
        side = T[i].cross(V(0, 0, 1))
        if side.length < .2:
            side = V(1, 0, 0)
        side.normalize()
        up = side.cross(T[i]).normalized()
        if up.z < 0:
            up = -up
        row_top, row_bot = [], []
        for j in range(segs_u + 1):
            u = j / segs_u - .5
            torn = (rng.uniform(.75, 1.15) if j in (0, segs_u) else 1)
            q = U(p) + side * (u * w * torn) + up * (-abs(u) * w * .45)
            row_top.append(pc.bm.verts.new(q + up * thick))
            row_bot.append(pc.bm.verts.new(q))
        rows.append((row_top, row_bot))
    pile, back = pc.slot('slab'), pc.slot('backing')
    for i in range(n - 1):
        a, b = rows[i], rows[i + 1]
        for j in range(segs_u):
            f = pc.bm.faces.new((a[0][j], a[0][j + 1], b[0][j + 1], b[0][j]))
            f.material_index = pile
            f = pc.bm.faces.new((a[1][j], b[1][j], b[1][j + 1], a[1][j + 1]))
            f.material_index = back
        for j, flip in ((0, False), (segs_u, True)):
            quad = (a[0][j], b[0][j], b[1][j], a[1][j])
            f = pc.bm.faces.new(tuple(reversed(quad)) if flip else quad)
            f.material_index = pile
    for ring, flip in ((rows[0], False), (rows[-1], True)):
        for j in range(segs_u):
            quad = (ring[0][j], ring[1][j], ring[1][j + 1], ring[0][j + 1])
            f = pc.bm.faces.new(tuple(reversed(quad)) if flip else quad)
            f.material_index = pile


def pile_clumps(hide, tier):
    """The carpet's pile: small flat overlapping tufts of matted wet carpet over the skin, hanging down the body: the texture and the
    fray up close. Each is a low open cap of three triangles rooted in the surface."""
    count = {0: 1400, 1: 560, 2: 0}[tier]
    if count == 0:
        return []
    deps = bpy.context.evaluated_depsgraph_get()
    ev = hide.ob.evaluated_get(deps)
    me = ev.to_mesh()
    me.calc_loop_triangles()
    areas = [t.area for t in me.loop_triangles]
    tris = list(me.loop_triangles)
    rng = random.Random(77)
    picks = rng.choices(range(len(tris)), weights=areas, k=count)
    pc = Piece('pile')
    faces = []
    for t in picks:
        tri = tris[t]
        a, b, c = [me.vertices[i].co for i in tri.vertices]
        r1, r2 = rng.random(), rng.random()
        if r1 + r2 > 1:
            r1, r2 = 1 - r1, 1 - r2
        p = a + (b - a) * r1 + (c - a) * r2
        n = Vector(tri.normal)
        fall = Vector((rng.uniform(-.35, .35), rng.uniform(-.35, .35), -1.0))
        lay = fall - n * fall.dot(n)
        if lay.length < .05:
            lay = n.orthogonal()
        lay.normalize()
        side = lay.cross(n).normalized()
        w = rng.uniform(.016, .034)
        L = rng.uniform(.03, .075)
        h = rng.uniform(.005, .013)
        root = p - lay * L * .35 - n * .004
        v0 = pc.bm.verts.new(root + side * w * .9)
        v1 = pc.bm.verts.new(root - side * w * .9)
        mid = pc.bm.verts.new(p + n * h + side * w * rng.uniform(-.3, .3))
        tip = pc.bm.verts.new(p + lay * L + side * w * rng.uniform(-.5, .5) + n * (h * .4))
        faces.append(pc.bm.faces.new((v0, tip, mid)))
        faces.append(pc.bm.faces.new((mid, tip, v1)))
        faces.append(pc.bm.faces.new((v1, v0, mid)))
    pc.tag(faces, 'slab')
    ev.to_mesh_clear()
    pc.sharp = 180.0
    ob = pc.make(recalc=False)
    return [Part(ob, bones=HIDE_BONES, minimum=10)]


def carpet_parts(tier):
    pc = Piece('slabs')
    slab(pc, [V(-.08, 3.72, -.10), V(-.34, 3.62, .02), V(-.62, 3.28, .10), V(-.76, 2.92, .14)], (.36, .34, .22, .10), .032, 11)
    slab(pc, [V(.08, 3.74, -.16), V(.36, 3.60, -.02), V(.60, 3.24, .08), V(.70, 2.98, .10)], (.36, .30, .20, .09), .032, 12)
    slab(pc, [V(-.26, 2.34, -.84), V(-.31, 2.04, -.88), V(-.34, 1.70, -.88), V(-.37, 1.30, -.86)], (.17, .16, .11, .04), .022, 13)
    slab(pc, [V(.10, 2.28, -.92), V(.12, 1.98, -.94), V(.13, 1.72, -.93), V(.15, 1.48, -.92)], (.13, .14, .09, .03), .022, 15)
    slab(pc, [V(.28, 2.32, -.82), V(.36, 2.04, -.86), V(.40, 1.80, -.86)], (.16, .13, .04), .022, 14)
    return [make(pc, bones=['clav-l', 'upper-l', 'chest', 'clav-r', 'upper-r', 'pelvis', 'thigh-l', 'thigh-r'], ratio=.8)]


def tatters(tier):
    """Short rags of pile along the limbs: the silhouette's fray up close."""
    if tier >= 2:
        return []
    out = []
    pc = Piece('tatters')
    rng = random.Random(5)
    count = 38 if tier == 0 else 20
    limbs = []
    for n in ('l', 'r'):
        limbs += [(at('upper-' + n), at('fore-' + n)), (at('fore-' + n), at('hand-' + n)), (at('thigh-' + n), at('shin-' + n)), (at('shin-' + n), at('foot-' + n))]
    for k in range(count):
        a, b = limbs[k % len(limbs)]
        t = rng.uniform(.08, .92)
        p = a.lerp(b, t)
        around = rng.uniform(0, 6.28)
        d = (b - a).normalized()
        s = d.cross(V(.3, .2, 1)).normalized()
        w = d.cross(s)
        off = (s * math.cos(around) + w * math.sin(around)) * rng.uniform(.03, .05)
        L = rng.uniform(.10, .26)
        hang = V(rng.uniform(-.05, .05), -1, rng.uniform(-.05, .05)).normalized()
        q0, q1, q2 = p + off, p + off * 1.5 + hang * L * .5, p + off * 1.7 + hang * L
        nrm = U(off.normalized())
        gk.ribbon(pc, [U(q0), U(q1), U(q2)], [nrm, nrm, nrm], .05, .008, 'slab', widths=[rng.uniform(.04, .07), rng.uniform(.03, .05), .006])
    out.append(make(pc, bones=['upper-l', 'fore-l', 'upper-r', 'fore-r', 'thigh-l', 'shin-l', 'thigh-r', 'shin-r'], ratio=1 if tier == 0 else .7))
    return out


# ---------------------------------------------------------------------------------------------------------------------------
# The wallpaper strips: long, peeling, two sided; one chain each
# ---------------------------------------------------------------------------------------------------------------------------
def strip_parts(tier):
    out = []
    across, down = {0: (4, 22), 1: (3, 14), 2: (2, 8)}[tier]
    for s, (parent, x, y, z, length, width) in enumerate(STRIP_DEFS):
        rng = random.Random(s * 31 + 2)
        pc = Piece('strip-%d' % s)
        top, bottom = [], []
        side = 1 if x > 0 else -1
        out_dir = V(side * .7, 0, -.7 if z < -.2 else .1).normalized()
        cross = V(0, 1, 0).cross(out_dir).normalized()
        colours = {}
        for i in range(down + 1):
            t = i / down
            # A tapered, torn length curling off at the tip and wandering a little, the plaster side showing where it turns.
            curl = .08 * math.sin(t * 5 + s) + .22 * t * t * t * (1 if s % 2 == 0 else -.6)
            w = width * (1 - .55 * t * t) * (1 + .22 * noise.noise(Vector((s * 2.0, t * 7, 0))))
            centre = V(x, y - length * t, z) + out_dir * curl
            twist = .5 * t * t * (1 if s % 3 else -1)
            cr = (cross * math.cos(twist) + out_dir * math.sin(twist)).normalized()
            nr = cr.cross(V(0, 1, 0)).normalized()
            rowt, rowb = [], []
            for j in range(across + 1):
                u = (j / across - .5) * w
                wave = .012 * math.sin(u * 40 + t * 11)
                p = centre + cr * u + nr * wave
                if i == down:
                    p = p + V(0, rng.uniform(-.07, .05), 0)
                vt = pc.bm.verts.new(U(p + nr * .003))
                vb = pc.bm.verts.new(U(p - nr * .003))
                rowt.append(vt)
                rowb.append(vb)
                colours[tuple(round(c, 4) for c in vt.co)] = chevron(u, length * t)
            top.append(rowt)
            bottom.append(rowb)
        fin_front, fin_back = pc.slot('paper'), pc.slot('plaster')
        for i in range(down):
            for j in range(across):
                f = pc.bm.faces.new((top[i][j], top[i][j + 1], top[i + 1][j + 1], top[i + 1][j]))
                f.material_index = fin_front
                f = pc.bm.faces.new((bottom[i][j], bottom[i + 1][j], bottom[i + 1][j + 1], bottom[i][j + 1]))
                f.material_index = fin_back
        for i in range(down):
            for row, j, flip in ((0, 0, True), (0, across, False)):
                quad = (top[i][j], bottom[i][j], bottom[i + 1][j], top[i + 1][j])
                f = pc.bm.faces.new(tuple(reversed(quad)) if flip else quad)
                f.material_index = fin_back
        p = make(pc, bones=['strip-%d-%d' % (s, k) for k in range(STRIP_LINKS)], minimum=10)
        out.append(p)
    return out


def strip_segments(s):
    parent, x, y, z, length, width = STRIP_DEFS[s]
    return [(V(x, y - length * k / STRIP_LINKS, z), V(x, y - length * (k + 1) / STRIP_LINKS, z)) for k in range(STRIP_LINKS)]


# ---------------------------------------------------------------------------------------------------------------------------
# The ribs: bent ceiling T-grid
# ---------------------------------------------------------------------------------------------------------------------------
def t_rail(pc, pts, fin, width=.042, web=.05, up=None, per=1):
    """A T-rail swept along the polyline (boss axes): the flange flat toward the outside, the web rising inward. Real bends: no smoothing."""
    P = [U(p) for p in pts]
    n = len(P)
    T = []
    for i in range(n):
        T.append((P[min(i + 1, n - 1)] - P[max(i - 1, 0)]).normalized())
    rings = []
    for i in range(n):
        centre = U(0, 3.05, -.14)
        # The flange's normal points away from the cage's axis.
        out = (P[i] - centre)
        out = (out - T[i] * out.dot(T[i])).normalized()
        s = T[i].cross(out).normalized()
        w = width * .5
        prof = [(-w, 0), (w, 0), (w, -.006), (.004, -.006), (.004, -web), (-.004, -web), (-.004, -.006), (-w, -.006)]
        rings.append([pc.bm.verts.new(P[i] + s * a + out * b) for a, b in prof])
    faces = []
    for i in range(n - 1):
        a, b = rings[i], rings[i + 1]
        for k in range(8):
            faces.append(pc.bm.faces.new((a[k], b[k], b[(k + 1) % 8], a[(k + 1) % 8])))
    faces.append(pc.bm.faces.new(list(reversed(rings[0]))))
    faces.append(pc.bm.faces.new(rings[-1]))
    return pc.tag(faces, fin)


def rib_parts(tier):
    out = []
    rng = random.Random(44)
    heights = (3.42, 3.26, 3.08, 2.88, 2.68)
    for s, name in ((-1, 'l'), (1, 'r')):
        pc = Piece('ribs-' + name)
        for k, y in enumerate(heights):
            # From the spine, out round the flank, forward, and stopping short of the breastbone: bent, kinked, not quite even.
            z0 = -.54 - .02 * k
            r = .43 - .022 * k
            kink = jitter(k * 2 + (s + 1), 3, .02)
            pts = [V(s * .07, y + .05, z0), V(s * .24, y + .03, z0 + .02), V(s * (r - .03), y + .00, z0 + .20) + kink,
                   V(s * r, y - .04, z0 + .42), V(s * (r - .04), y - .10, z0 + .63) + kink * .6,
                   V(s * (r - .17), y - .15 - .03 * rng.random(), z0 + .78), V(s * (.03 + .05 * rng.random() + (k == 2) * .06), y - .19, z0 + .84)]
            t_rail(pc, pts, 'rail' if (k + (s > 0)) % 2 == 0 else 'rail-dark', width=.056, web=.05)
        # A cross tee riveted across two of them.
        t_rail(pc, [V(s * .40, 3.26, -.14), V(s * .41, 3.02, -.14), V(s * .385, 2.72, -.14)], 'rail-dark', width=.03, web=.03)
        for k in (0, 2):
            gk.cyl(pc, U(s * .40, heights[k] - .02, -.12), U(s * .44, heights[k] - .02, -.12), .008, 'steel', segs=6)
        out.append(make(pc, rigid='rib-' + name, ratio=.9, minimum=120))
    return out


def tile_piece(pc, centre, w, h, tilt, roll, seed):
    """A broken mineral ceiling tile: two square corners and a fractured edge, a little warped, with thickness; it hangs by a wire."""
    rng = random.Random(seed)
    ring = [(-w * .5, h * .5), (w * .5, h * .5)]
    # Down the right side a little, then a jagged break back across the bottom, up the left.
    ring.append((w * .5, h * rng.uniform(-.05, .2)))
    steps = 5
    for i in range(1, steps):
        t = i / steps
        ring.append((w * (.5 - t), -h * .5 + h * rng.uniform(0, .34) + (h * .12 if i % 2 else 0)))
    ring.append((-w * .5, -h * rng.uniform(.0, .3)))
    m = Matrix.Translation(U(centre)) @ Matrix.Rotation(roll, 4, 'Y') @ Matrix.Rotation(tilt, 4, 'X')
    n = len(ring)
    cx = sum(x for x, _ in ring) / n
    cz = sum(z for _, z in ring) / n
    ct = pc.bm.verts.new(m @ Vector((cx, .013, cz)))
    cb = pc.bm.verts.new(m @ Vector((cx, -.013, cz)))
    top = [pc.bm.verts.new(m @ Vector((x, .012, z))) for x, z in ring]
    bot = [pc.bm.verts.new(m @ Vector((x, -.012, z))) for x, z in ring]
    faces = []
    for i in range(n):
        k = (i + 1) % n
        faces.append(pc.bm.faces.new((ct, top[k], top[i])))
        faces.append(pc.bm.faces.new((cb, bot[i], bot[k])))
        faces.append(pc.bm.faces.new((top[k], bot[k], bot[i], top[i])))
    pc.tag(faces, 'tile')


def tile_parts(tier):
    out = []
    specs = ((.26, .32, 1.1, .35), (.2, .22, 1.3, -.25), (.28, .26, 1.2, -.55), (.2, .24, 1.25, .4))
    for i, (parent, x, y, z) in enumerate(TILE_DEFS):
        pc = Piece('tile-%d' % i)
        w, h, tilt, roll = specs[i]
        c = V(x + (.05 if x > 0 else -.05), y - h * .5 - .03, z)
        tile_piece(pc, c, w, h, tilt, roll, 60 + i)
        gk.tube(pc, [U(x, y + .04, z), U(c.x, y - .03, c.z)], .004, 'wire', segs=4)
        out.append(make(pc, rigid='tile-%d' % i, minimum=20))
    return out


# ---------------------------------------------------------------------------------------------------------------------------
# The Ballast: a transformer in the ribcage, hot amber through the ribs
# ---------------------------------------------------------------------------------------------------------------------------
def ballast_solid(tier):
    pc = Piece('ballast')
    c = at('ballast')
    # A laminated E-I core: stacked steel plates round two square windows, bolts through its corners, a cracked resin cap, brackets
    # and cables up to the spine.
    plates = 9 if tier == 0 else 5
    for i in range(plates):
        z = -.13 + .26 * i / (plates - 1)
        gk.box(pc, U(c + V(0, .17, z)), (.42, .022, .06), 'core' if i % 2 else 'ballast-case')
        gk.box(pc, U(c + V(0, -.17, z)), (.42, .022, .06), 'core' if i % 2 else 'ballast-case')
        gk.box(pc, U(c + V(0, 0, z)), (.07, .022, .30), 'core' if i % 2 else 'ballast-case')
        for s in (-1, 1):
            gk.box(pc, U(c + V(s * .19, 0, z)), (.05, .022, .30), 'core' if i % 2 else 'ballast-case')
    gk.box(pc, U(c + V(0, .225, -.04)), (.34, .2, .05), 'ballast-case')
    for s in (-1, 1):
        for sy in (-1, 1):
            gk.cyl(pc, U(c + V(s * .19, sy * .17, -.16)), U(c + V(s * .19, sy * .17, .16)), .011, 'steel', segs=6)
        gk.tube(pc, gk.catmull(path(c + V(s * .17, .24, -.1), c + V(s * .10, .40, -.26), V(s * .05, 3.40, -.44)), per=4), .014, 'wire', segs=6)
        gk.tube(pc, gk.catmull(path(c + V(s * .12, -.2, -.1), c + V(s * .18, -.38, -.30), V(s * .08, 2.66, -.62)), per=4), .012, 'wire-red', segs=6)
    gk.box(pc, U(c + V(0, 0, -.1)), (.34, .02, .26), 'ballast-case')
    return [make(pc, rigid='ballast', ratio=.9, minimum=60)]


def ballast_glow(tier):
    pc = Piece('ballast-glow')
    c = at('ballast')
    # The windings in the core's two windows: packed turns of copper, burning; a hot glow behind them.
    turns = {0: 8, 1: 5, 2: 3}[tier]
    for s in (-1, 1):
        for i in range(turns):
            y = -.115 + .23 * i / (turns - 1)
            gk.cyl(pc, U(c + V(s * .04, y, .115)), U(c + V(s * .16, y, .115)), .0135, 'glow-coil', segs=6 if tier == 0 else 4)
        gk.box(pc, U(c + V(s * .1, 0, .085)), (.12, .01, .26), 'glow-amber')
    return make(pc, rigid='ballast', ratio=1, minimum=40)


# ---------------------------------------------------------------------------------------------------------------------------
# The seven tubes down its spine, in their ballast housings
# ---------------------------------------------------------------------------------------------------------------------------
def tube_solid(i, tier):
    pc = Piece('tube-%d' % i)
    c, run, out = tube_frame(i)
    # Local axes: a along the tube, b across it, n off the back.
    a, n = U(run), U(out)
    a.normalize()
    b = a.cross(n).normalized()
    n = b.cross(a).normalized()
    rot = Matrix((b, a, n)).transposed()
    C = U(c)
    # A channel housing (enamel flaking), two end caps, the sockets, stand-offs into the hide.
    gk.box(pc, C + n * .025, (.09, TUBE_LENGTH * .96, .055), 'enamel-dark', rot=rot)
    gk.box(pc, C + n * .058, (.055, TUBE_LENGTH * .62, .012), 'rail-dark', rot=rot)
    for sd in (-1, 1):
        p = C + a * (sd * TUBE_LENGTH * .47)
        gk.box(pc, p + n * .05, (.05, .03, .05), 'socket', rot=rot)
        gk.cyl(pc, p - n * .07, p + n * .01, .017, 'steel', segs=6)
    # Four screws, and a lead from the socket into the back.
    for sd in (-1, 1):
        gk.cyl(pc, C + a * (sd * .2) + b * .03 + n * .054, C + a * (sd * .2) + b * .03 + n * .062, .007, 'alu', segs=6)
    return make(pc, rigid='tube-%d' % i, ratio=.9, minimum=40)


def tube_glow(i, tier):
    pc = Piece('tube-glow-%d' % i)
    c, run, out = tube_frame(i)
    a, n = U(run), U(out)
    a.normalize()
    b = a.cross(n).normalized()
    n = b.cross(a).normalized()
    C = U(c) + n * .092
    L = TUBE_LENGTH * .45
    gk.cyl(pc, C - a * L, C + a * L, .021, 'glow-tube', segs=10 if tier == 0 else 6)
    return make(pc, rigid='tube-%d' % i, minimum=12)


def tube_ends(i, tier):
    """The tube's aluminium end caps and pins: solid."""
    pc = Piece('tube-ends-%d' % i)
    c, run, out = tube_frame(i)
    a, n = U(run), U(out)
    a.normalize()
    b = a.cross(n).normalized()
    n = b.cross(a).normalized()
    C = U(c) + n * .092
    L = TUBE_LENGTH * .45
    for sd in (-1, 1):
        gk.cyl(pc, C + a * (sd * L), C + a * (sd * (L + .026)), .0225, 'alu', segs=8)
    return make(pc, rigid='tube-%d' % i, minimum=10)


def wire_parts(tier):
    out = []
    if tier >= 2:
        return out
    for i in range(TUBES):
        pc = Piece('wire-%d' % i)
        p0 = at('wire-%d-0' % i)
        for w, (fin, ox) in enumerate((('wire-red', -.008), ('wire-white', .008), ('wire', 0))):
            pts = []
            n = 7 if tier == 0 else 4
            for k in range(n + 1):
                t = k / n
                L = WIRE_LENGTH * WIRE_LINKS * (1 - w * .12)
                pts.append(U(p0 + V(ox + .012 * math.sin(t * 9 + w * 2), -L * t, .01 * math.cos(t * 7 + w))))
            gk.tube(pc, pts, .0055, fin, segs=5)
            # Bare copper at the tips.
            gk.tube(pc, [pts[-1], pts[-1] + Vector((.004, 0, -.03))], .0034, 'copper', segs=4)
        out.append(make(pc, bones=['wire-%d-0' % i, 'wire-%d-1' % i], minimum=12))
    return out


# ---------------------------------------------------------------------------------------------------------------------------
# The fingers: a metre long, jointed lengths of red-brown baseboard and bent T-rail, baseboard on the first link
# ---------------------------------------------------------------------------------------------------------------------------
def board_link(pc, p0, p1, width, thick, fin, tip=False, up=None):
    """A length of moulded baseboard from p0 to p1 (boss axes), its face up: a stepped profile, splintered at the tip."""
    a, b = U(p0), U(p1)
    d = (b - a).normalized()
    side = d.cross(Vector((0, 0, 1)))
    if side.length < .2:
        side = d.cross(Vector((0, 1, 0)))
    side.normalize()
    n = side.cross(d).normalized()
    if n.z < 0:
        n = -n
    prof = [(-.5, 0), (.5, 0), (.5, .7), (.28, .85), (.28, 1.0), (-.5, 1.0)]
    rings = []
    steps = 3
    for k in range(steps + 1):
        t = k / steps
        taper = 1 - (.55 * t if tip else 0)
        c = a.lerp(b, t)
        rings.append([pc.bm.verts.new(c + side * (x * width * taper) + n * ((y - .5) * thick * taper)) for x, y in prof])
    faces = []
    m = len(prof)
    for k in range(steps):
        for i in range(m):
            faces.append(pc.bm.faces.new((rings[k][i], rings[k][(i + 1) % m], rings[k + 1][(i + 1) % m], rings[k + 1][i])))
    faces.append(pc.bm.faces.new(list(reversed(rings[0]))))
    faces.append(pc.bm.faces.new(rings[-1]))
    pc.tag(faces, fin)


def finger_parts(tier):
    out = []
    # (h 2 is the Wall Hand's: the right hand's fingers on its own joints.)
    for h, (s, n) in enumerate(((-1, 'l'), (1, 'r'), (1, 'r'))):
        if h == 2 and tier == 2:
            continue
        for f in range(4):
            for k in range(3):
                name = 'finger-%s%d-%d' % (n, f, k)
                bone = name if h < 2 else 'wall-finger-%d-%d' % (f, k)
                pc = Piece(name + ('-w' if h == 2 else ''))
                p0 = at(name)
                p1 = at('finger-%s%d-%d' % (n, f, k + 1)) if k < 2 else None
                yaw = math.radians(FINGER_YAW[f] * s) + math.radians((k - 1) * FINGER_YAW[f] * s * .22)
                pitch = math.radians(FINGER_PITCH[f][k])
                d = V(math.sin(yaw) * math.cos(pitch), math.sin(pitch), math.cos(yaw) * math.cos(pitch))
                if p1 is None:
                    p1 = p0 + d * FINGER_LINKS[f][k]
                if k == 0:
                    board_link(pc, p0 - d * .02, p1 + d * .015, .056, .032, 'board', False)
                    # The hide runs up the first link: the board grows out of a wet sleeve.
                    rope(pc, path(p0 - d * .02, p0 + d * .10, p0 + d * .21), (.040, .036, .012), per=2, segs=6)
                elif k == 1:
                    if (f + h) % 2 == 0:
                        board_link(pc, p0 - d * .012, p1 + d * .01, .048, .026, 'board-dark', False)
                    else:
                        # A length of bent T-rail in place of the board.
                        bent = p0 * .5 + p1 * .5 + V(0, .03, 0)
                        t_rail(pc, [p0 - d * .012, bent, p1 + d * .01], 'rail-dark', width=.034, web=.026)
                else:
                    board_link(pc, p0 - d * .012, p1 + d * .012, .04, .022, 'board-dark' if f % 2 else 'board', True)
                # The knuckle: a rusted bolt through it.
                side = d.cross(V(0, 1, 0)).normalized()
                gk.cyl(pc, U(p0 - side * .032), U(p0 + side * .032), .0095, 'steel', segs=6)
                out.append(make(pc, rigid=bone, ratio=.85 if tier == 1 else (.6 if tier == 2 else 1), minimum=10))
    return out


def wall_arm(tier):
    """The forearm and hand of the arm that comes out of the wall: ropes of hide, no voxel skin (it's half in the paper)."""
    pc = Piece('wall-arm')
    el, wr = at('wall-fore'), at('wall-hand')
    b = (wr - el).normalized()
    side = b.cross(V(0, 0, 1)).normalized()
    out = b.cross(side).normalized()
    seg = 8 if tier == 0 else 5
    for k in (0, 1):
        ph = k * math.pi
        pts, rad = [], []
        for i in range(7):
            t = i / 6
            ang = ph + t * 3.2
            off = (side * math.cos(ang) + out * math.sin(ang)) * (.042 - .018 * t)
            pts.append(U(el + (wr - el) * (.02 + t * .96) + off))
            rad.append(max(.03, .062 * (1 - .5 * t)))
        rope(pc, pts, rad, per=3, segs=seg)
    knob(pc, U(wr), (.052, .042, .06))
    for f in range(4):
        k0 = at('wall-finger-%d-0' % f)
        rope(pc, path(wr + V(0, -.005, 0), wr * .45 + k0 * .55 + V(0, .018, 0), k0 + V(0, .0, .015)), (.034, .03, .036), per=3, segs=seg)
    knob(pc, U(wr + V(0, .03, .11)), (.11, .03, .10))
    return [make(pc, bones=['wall-fore', 'wall-hand'], ratio=.8, minimum=120)]


# ---------------------------------------------------------------------------------------------------------------------------
# The head: one real recessed fixture
# ---------------------------------------------------------------------------------------------------------------------------
HEAD_W, HEAD_H, HEAD_D = 1.6, .4, .3


def head_solid(tier):
    pc = Piece('head')
    c = at('head')
    w, h, d = HEAD_W, HEAD_H, HEAD_D
    fz = c.z + d * .5
    bz = c.z - d * .5
    # The pan: a five sided steel trough, open at the front, a flange round its face, stepped ends.
    wall = .012
    gk.box(pc, U(c.x, c.y, bz + wall * .5), (w, wall, h), 'enamel')
    for sd in (-1, 1):
        gk.box(pc, U(c.x, c.y + sd * (h * .5 - wall * .5), c.z), (w, d, wall), 'enamel')
        gk.box(pc, U(c.x + sd * (w * .5 - wall * .5), c.y, c.z), (wall, d, h), 'enamel')
    # The flange (it would sit flush in the ceiling).
    for sd in (-1, 1):
        gk.box(pc, U(c.x, c.y + sd * (h * .5 + .012), fz - .004), (w + .05, .008, .03), 'enamel')
        gk.box(pc, U(c.x + sd * (w * .5 + .012), c.y, fz - .004), (.03, .008, h + .03), 'enamel')
    # Inside: the white reflector, its folded peak behind the tubes, the sockets.
    gk.box(pc, U(c.x, c.y, bz + .03), (w - .04, .012, h - .04), 'enamel')
    for sd in (-1, 1):
        gk.box(pc, U(c.x, c.y + sd * .06, bz + .058), (w - .08, .036, .012), 'enamel-dark', rot=Matrix.Rotation(sd * .5, 3, 'X'))
        for ey in (-1, 1):
            gk.box(pc, U(c.x + sd * (w * .5 - .05), c.y + ey * .08, bz + .10), (.05, .08, .05), 'socket')
    # The back: the ballast cover riveted on, knockouts, hanger clips, and the flex conduit it grows from.
    gk.box(pc, U(c.x, c.y, bz - .025), (w * .5, .04, .15), 'enamel-dark')
    for x in (-.55, -.3, .3, .55):
        gk.cyl(pc, U(c.x + x, c.y + .10, bz - .004), U(c.x + x, c.y + .10, bz - .012), .018, 'steel', segs=8)
        gk.cyl(pc, U(c.x + x, c.y - .12, bz - .004), U(c.x + x, c.y - .12, bz - .012), .012, 'steel', segs=8)
    for sd in (-1, 1):
        for k in range(6):
            gk.cyl(pc, U(c.x + sd * (.06 + k * .04), c.y + .06, bz - .046), U(c.x + sd * (.06 + k * .04), c.y + .06, bz - .05), .005, 'alu', segs=5)
        # A bent hanger: a strap of T-rail still on the top, the ceiling it was torn out of.
        gk.box(pc, U(c.x + sd * .62, c.y + h * .5 + .03, c.z - .03), (.03, .24, .05), 'rail-dark', rot=Matrix.Rotation(sd * .3, 3, 'Z'))
    # A dark gasket framing the lit panel, so the bar has an edge, and the rim's screws.
    for sd in (-1, 1):
        gk.box(pc, U(c.x, c.y + sd * (h * .5 - .002), fz + .006), (w - .01, .012, .012), 'enamel-dark')
        gk.box(pc, U(c.x + sd * (w * .5 - .002), c.y, fz + .006), (.012, .012, h), 'enamel-dark')
        for x in (-.7, -.35, .0, .35, .7):
            gk.cyl(pc, U(c.x + x, c.y + sd * (h * .5 + .012), fz + .009), U(c.x + x, c.y + sd * (h * .5 + .012), fz + .015), .006, 'alu', segs=5)
    out = [make(pc, rigid='head', ratio=.9, minimum=80)]
    # What came with it when it tore out of the ceiling: stubs of T-grid on the top and a corner of a stained tile behind the rim.
    ce = Piece('head-ceiling')
    t_rail(ce, [V(-.86, c.y + h * .5 + .05, bz - .02), V(-.30, c.y + h * .5 + .06, bz - .03), V(.34, c.y + h * .5 + .05, bz - .02), V(.92, c.y + h * .5 + .09, bz - .05)], 'rail-dark', width=.04, web=.03)
    t_rail(ce, [V(-.55, c.y + h * .5 + .05, bz - .02), V(-.60, c.y + h * .5 + .08, bz - .34), V(-.58, c.y + h * .5 + .12, bz - .50)], 'rail-dark', width=.034, web=.026)
    tile_piece(ce, V(.34, c.y + h * .5 + .075, bz - .24), .42, .36, .08, .1, 71)
    tile_piece(ce, V(-.28, c.y + h * .5 + .08, bz - .20), .30, .26, -.1, -.14, 72)
    out.append(make(ce, rigid='head', ratio=.9, minimum=30))
    # Cables and conduit from the back of the pan into the neck.
    co = Piece('head-cables')
    neck = at('neck2')
    gk.tube(co, gk.catmull(path(V(-.10, c.y + .02, bz - .03), V(-.09, c.y + .04, bz - .14), neck + V(-.05, .03, -.08), at('neck1') + V(-.03, .05, 0)), per=5), .022, 'conduit', segs=8)
    gk.tube(co, gk.catmull(path(V(.10, c.y - .02, bz - .03), V(.09, c.y - .05, bz - .14), neck + V(.05, -.01, -.08), at('neck1') + V(.03, -.04, 0)), per=5), .018, 'wire', segs=6)
    gk.tube(co, gk.catmull(path(V(.0, c.y - .06, bz - .03), V(.0, c.y - .10, bz - .16), neck + V(0, -.07, -.05), at('neck1') + V(.0, -.07, 0)), per=5), .012, 'wire-red', segs=6)
    out.append(make(co, bones=['head', 'neck2', 'neck1'], ratio=.85, minimum=40))
    return out


def head_glow(tier):
    """The two tubes behind the diffuser."""
    pc = Piece('head-tubes')
    c = at('head')
    z = c.z - HEAD_D * .5 + .11
    for sd in (-1, 1):
        gk.cyl(pc, U(c.x - HEAD_W * .5 + .06, c.y + sd * .08, z), U(c.x + HEAD_W * .5 - .06, c.y + sd * .08, z), .024, 'glow-tube', segs=12 if tier == 0 else 6)
    return make(pc, rigid='head', minimum=20)


def diffuser_half(upper, tier):
    """One half of the prismatic diffuser: a grid of little pyramids on a lit panel, hinged on the pan's rim. Glowing."""
    c = at('head')
    name = 'jaw-u' if upper else 'jaw-l'
    pc = Piece('diffuser-' + name)
    fz = c.z + HEAD_D * .5
    h = HEAD_H * .5 - .005
    nx, ny = {0: (44, 5), 1: (20, 3), 2: (6, 1)}[tier]
    w = HEAD_W - .03
    y0 = c.y + (.003 if upper else -.003)
    verts = {}

    def vert(i, j):
        if (i, j) not in verts:
            x = c.x + w * (i / nx - .5)
            y = y0 + (1 if upper else -1) * h * (j / ny)
            verts[i, j] = pc.bm.verts.new(U(x, y, fz))
        return verts[i, j]
    faces = []
    for i in range(nx):
        for j in range(ny):
            q = (vert(i, j), vert(i + 1, j), vert(i + 1, j + 1), vert(i, j + 1))
            if tier < 2:
                # The pyramid's peak.
                x = c.x + w * ((i + .5) / nx - .5)
                y = y0 + (1 if upper else -1) * h * ((j + .5) / ny)
                peak = pc.bm.verts.new(U(x, y, fz + .007))
                for k in range(4):
                    faces.append(pc.bm.faces.new((q[k], q[(k + 1) % 4], peak)))
            else:
                faces.append(pc.bm.faces.new(q))
    pc.tag(faces, 'glow-diffuser')

    def panel(boss):
        # Hot along the two tubes, dimmer at the rims and the ends; yellowed plastic with a stain and specks low down.
        dy = abs(abs(boss.y - c.y) - .08)
        k = .62 + .38 * math.exp(-(dy / .07) ** 2)
        k *= .8 + .2 * _smooth(0, .22, HEAD_W * .5 - abs(boss.x))
        stain = noise.noise(Vector((boss.x * 5, boss.y * 9, 2.0)))
        if not upper:
            k *= 1 - .3 * _smooth(.05, .4, stain) * _smooth(.05, .19, c.y - boss.y)
        return k
    return Part(pc.make(recalc=False), rigid=name, minimum=20, colour=panel)


def diffuser_back(upper, tier):
    """The back of the lid and its row of broken tube glass: the teeth, seen when the jaw opens. Solid dark steel and pale shards."""
    c = at('head')
    name = 'jaw-u' if upper else 'jaw-l'
    pc = Piece('lid-' + name)
    sg = 1 if upper else -1
    fz = c.z + HEAD_D * .5
    h = HEAD_H * .5 - .005
    y = c.y + sg * h * .5
    gk.box(pc, U(c.x, y, fz - .006), (HEAD_W - .03, .008, h), 'enamel-dark')
    rng = random.Random(90 if upper else 91)
    shards = {0: 34, 1: 18, 2: 8}[tier]
    for i in range(shards):
        x = c.x + (HEAD_W - .08) * ((i + rng.random() * .6) / shards - .5)
        L = rng.uniform(.05, .13) * (1.25 if abs(x) < .35 else 1)
        base = V(x, c.y + sg * (.006 + rng.uniform(0, .02)), fz - .012)
        tip = base + V(rng.uniform(-.018, .018), -sg * L * .25, -L)
        # A curved shard of tube wall: a thin splinter.
        r = rng.uniform(.012, .024)
        for j in range(1):
            verts = [pc.bm.verts.new(U(base + V(math.cos(a) * r, math.sin(a) * r * .4, 0))) for a in (0, 2.1, 4.2)]
            t = pc.bm.verts.new(U(tip))
            fs = [pc.bm.faces.new((verts[0], verts[1], t)), pc.bm.faces.new((verts[1], verts[2], t)), pc.bm.faces.new((verts[2], verts[0], t)), pc.bm.faces.new(list(reversed(verts)))]
            pc.tag(fs, 'glass')
    return make(pc, rigid=name, minimum=20)


# ===========================================================================================================================
# The bake: colours, ambient occlusion, skin weights
# ===========================================================================================================================
def tier_ratio(part, tier):
    return part.ratio


def finish_decimate(part, tier):
    """The far tiers: a collapse, but every solid keeps a little shape (Decimate collapses small solids into spikes otherwise)."""
    ob = part.ob
    deps = bpy.context.evaluated_depsgraph_get()
    faces = len(ob.evaluated_get(deps).to_mesh().polygons)
    keep = {0: 1.0, 1: .5, 2: .2}[tier] * part.ratio
    target = max(part.minimum, int(faces * keep))
    if target < faces:
        m = ob.modifiers.new('dec', 'DECIMATE')
        m.ratio = target / faces
        m.use_collapse_triangulate = True


RAYS = []


def _rays():
    if RAYS:
        return
    n = 14
    for i in range(n):
        # A Fibonacci hemisphere.
        z = 1 - (i + .5) / n
        r = math.sqrt(max(0, 1 - z * z))
        a = i * 2.39996
        RAYS.append(Vector((r * math.cos(a), r * math.sin(a), z)))


def segment_distance(p, a, b):
    ab = b - a
    l2 = ab.length_squared
    if l2 < 1e-9:
        return (p - a).length
    t = max(0.0, min(1.0, (p - a).dot(ab) / l2))
    return (p - (a + ab * t)).length


def weights_of(p_boss, bones, segs):
    """The four nearest bones by distance (steep inverse power), summing to 1."""
    scored = []
    for b in bones:
        a0, a1 = segs[b]
        d = max(.012, segment_distance(p_boss, a0, a1))
        scored.append((1.0 / (d ** 7), b))
    scored.sort(reverse=True)
    top = scored[:4]
    s = sum(w for w, _ in top)
    res = [(w / s, b) for w, b in top if w / s > .012]
    s2 = sum(w for w, _ in res)
    return [(w / s2, b) for w, b in res]


def all_segments():
    segs = {}
    for name, parent, p in JOINTS:
        segs[name] = segment_of(name)
    for s in range(STRIPS):
        parts = strip_segments(s)
        for k in range(STRIP_LINKS):
            segs['strip-%d-%d' % (s, k)] = parts[k]
    for i in range(TUBES):
        p0 = at('wire-%d-0' % i)
        for k in range(WIRE_LINKS):
            segs['wire-%d-%d' % (i, k)] = (p0 + V(0, -WIRE_LENGTH * k, 0), p0 + V(0, -WIRE_LENGTH * (k + 1), 0))
    return segs


def bake(parts, name, seed=0.0, ao_scene=None, skinned=True):
    """The parts as one GearMesh in model space (Unity axes): colours with the ambient occlusion from `ao_scene` (a BVH of the whole tier
    in the design frame), skin weights in uv0 and uv1 when `skinned`."""
    _rays()
    gm = gk.GearMesh(name)
    deps = bpy.context.evaluated_depsgraph_get()
    segs = all_segments()
    flip = TO_MODEL.to_3x3().determinant() < 0
    index = {}
    for part in parts:
        ob = part.ob
        ev = ob.evaluated_get(deps)
        me = ev.to_mesh()
        me.calc_loop_triangles()
        cav = gk._cavity(me)
        cn = me.corner_normals
        W = ob.matrix_world
        Wn = W.to_3x3().inverted().transposed()
        M = TO_MODEL @ W
        Mn = M.to_3x3().inverted().transposed()
        ao_cache = {}
        w_cache = {}
        for tri in me.loop_triangles:
            mat = ob.material_slots[tri.material_index].material if ob.material_slots else None
            f = gk.FINISHES[mat.name] if mat is not None else next(iter(gk.FINISHES.values()))
            ids = []
            for li in tri.loops:
                vi = me.loops[li].vertex_index
                co = me.vertices[vi].co
                pd = W @ co
                nd = (Wn @ cn[li].vector).normalized()
                ao = 1.0
                if ao_scene is not None and not f.glow:
                    ao = ao_cache.get(vi)
                    if ao is None:
                        vn = (Wn @ me.vertices[vi].normal).normalized()
                        q = Vector((0, 0, 1)).rotation_difference(vn).to_matrix()
                        hits = 0.0
                        org = pd + vn * .012
                        for r in RAYS:
                            loc, _, _, dist = ao_scene.ray_cast(org, q @ r, .55)
                            if loc is not None:
                                hits += 1.0 - dist / .55
                        ao = max(0.0, 1.0 - 1.45 * hits / len(RAYS))
                        ao_cache[vi] = ao
                boss = Vector((pd.x, pd.z, pd.y))
                extra = None
                if f.kind == 'paper':
                    # The pattern in the strip's own coordinates (its width across, its fall down).
                    extra = chevron(boss.x * 1.0 + boss.z * .7, boss.y)
                if f.glow and part.colour is not None:
                    extra = part.colour(boss)
                col = shade(f, pd, nd, cav[vi], ao, seed, extra)
                pu = M @ co
                nu = (Mn @ cn[li].vector).normalized()
                rgba = (round(col.x * 255), round(col.y * 255), round(col.z * 255), 255 if f.outline else 0)
                wk = w_cache.get(vi)
                if wk is None:
                    if part.rigid is not None:
                        wk = ((1.0, part.rigid),)
                    elif skinned:
                        wk = tuple(weights_of(boss, part.bones, segs))
                    else:
                        wk = ()
                    w_cache[vi] = wk
                uvw = [0.0, 0.0, 0.0, 0.0]
                for i, (w, b) in enumerate(wk[:4]):
                    uvw[i] = INDEX[b] + min(.998, w * .998)
                # (The weights belong in the key: the Wall Hand's fingers lie exactly on the right hand's, on other bones.)
                key = (round(pu.x, 4), round(pu.y, 4), round(pu.z, 4), round(nu.x, 2), round(nu.y, 2), round(nu.z, 2), rgba, tuple(round(u, 3) for u in uvw))
                k = index.get(key)
                if k is None:
                    k = index[key] = len(gm.positions)
                    gm.positions.append((pu.x, pu.y, pu.z))
                    gm.normals.append((nu.x, nu.y, nu.z))
                    gm.uv0.append((uvw[0], uvw[1]))
                    gm.uv1.append((uvw[2], uvw[3]))
                    gm.colors.append(tuple(c / 255.0 for c in rgba))
                ids.append(k)
            if flip:
                ids = [ids[0], ids[2], ids[1]]
            gm.submeshes[1 if f.glow else 0].extend(ids)
        if os.environ.get('GEAR_PARTS'):
            print('   %-20s %6d triangles' % (ob.name, len(me.loop_triangles)))
        ev.to_mesh_clear()
    return gm


def scene_bvh(parts):
    deps = bpy.context.evaluated_depsgraph_get()
    verts, polys = [], []
    for part in parts:
        ob = part.ob
        ev = ob.evaluated_get(deps)
        me = ev.to_mesh()
        W = ob.matrix_world
        base = len(verts)
        verts += [W @ v.co for v in me.vertices]
        polys += [[base + i for i in p.vertices] for p in me.polygons]
        ev.to_mesh_clear()
    return BVHTree.FromPolygons(verts, polys, all_triangles=False)


def build_tier(tier):
    """Every solid part and every lit part of a tier, as objects."""
    gk.clear_scene()
    hide = build_hide(tier)
    solids = [hide] + pile_clumps(hide, tier)
    solids += (wall_arm(tier) if tier < 2 else []) + toe_parts(tier) + carpet_parts(tier) + tatters(tier) + strip_parts(tier) + rib_parts(tier) + tile_parts(tier) + ballast_solid(tier) + finger_parts(tier) + head_solid(tier) + wire_parts(tier)
    solids += [tube_solid(i, tier) for i in range(TUBES)] + [tube_ends(i, tier) for i in range(TUBES)]
    solids += [diffuser_back(True, tier), diffuser_back(False, tier)]
    glows = {}
    for i in range(TUBES):
        glows['tube-%d' % i] = [tube_glow(i, tier)]
    glows['head'] = [head_glow(tier)]
    glows['jaw-u'] = [diffuser_half(True, tier)]
    glows['jaw-l'] = [diffuser_half(False, tier)]
    glows['ballast'] = [ballast_glow(tier)]
    if tier > 0:
        for p in solids:
            if p.ob.name != 'hide':
                finish_decimate(p, tier)
    return solids, glows


def write(bank, tier, solids, glows, sheet_cb=None):
    t0 = time_now()
    bvh = scene_bvh(solids)
    gm = bake(solids, 'hum-body-%d' % tier, ao_scene=bvh)
    gm.submeshes = [gm.submeshes[0]]
    bank.add('hum-body-%d' % tier, 'root', gm)
    tris = len(gm.submeshes[0]) // 3
    out = {'body': gm}
    for key, parts in glows.items():
        g = bake(parts, 'hum-%s-%d' % (key, tier), skinned=False)
        g.submeshes = [g.submeshes[1]]
        g.uv1 = []
        mi = bank.bank.mesh('hum-%s-%d' % (key, tier), g, [bank.glow])
        bank.bank.prop('hum-%s-%d' % (key, tier), key, [(mi, [1.0, 0, 0, 0, 0, 1.0, 0, 0, 0, 0, 1.0, 0])])
        tris += len(g.submeshes[0]) // 3
        out[key] = g
        print('glow %-22s %6d triangles' % (key, len(g.submeshes[0]) // 3))
    print('tier %d: %d triangles (budget %d), %.1f s' % (tier, tris, BUDGET[tier], time_now() - t0))
    return out, tris


def time_now():
    import time
    return time.time()


# ===========================================================================================================================
# The review sheet: what the bank holds, rendered back in the design frame
# ===========================================================================================================================
def sheet(baked, out_dir, tier):
    os.makedirs(out_dir, exist_ok=True)
    gk.clear_scene()
    inv = TO_MODEL.inverted()

    def to_ob(gm, name, scale=1.0, tint=None):
        me = bpy.data.meshes.new(name)
        verts = [tuple(inv @ Vector(p)) for p in gm.positions]
        tris = []
        for sub in gm.submeshes:
            for i in range(0, len(sub), 3):
                t = sub[i:i + 3]
                tris.append((t[0], t[2], t[1]))
        me.from_pydata(verts, [], tris)
        attr = me.color_attributes.new('Col', 'BYTE_COLOR', 'POINT')
        for i, c in enumerate(gm.colors):
            col = (c[0] * scale, c[1] * scale, c[2] * scale, 1.0)
            if tint is not None:
                col = tint
            attr.data[i].color_srgb = col
        for p in me.polygons:
            p.use_smooth = True
        ob = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(ob)
        return ob
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    sh = scene.display.shading
    sh.color_type = 'VERTEX'
    sh.show_cavity = False
    sh.background_type = 'VIEWPORT'
    scene.render.film_transparent = False
    scene.view_settings.view_transform = 'Standard'
    scene.view_settings.exposure = 1.6
    scene.render.resolution_x, scene.render.resolution_y = 860, 860
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam'))
    scene.collection.objects.link(cam)
    scene.camera = cam
    obs = [to_ob(gm, k) for k, gm in baked.items()]
    # Room: a floor and a ceiling so the height reads.
    fl = bpy.data.meshes.new('floor')
    fl.from_pydata([(-14, -14, 0), (14, -14, 0), (14, 14, 0), (-14, 14, 0), (-14, -14, 4), (14, -14, 4), (14, 14, 4), (-14, 14, 4)], [], [(0, 1, 2, 3), (7, 6, 5, 4)])
    a = fl.color_attributes.new('Col', 'BYTE_COLOR', 'POINT')
    for i in range(8):
        a.data[i].color_srgb = (.46, .42, .22, 1)
    flo = bpy.data.objects.new('floor', fl)
    scene.collection.objects.link(flo)

    def shot(name, eye, look, lens, bg=(.72, .68, .36), light='FLAT' if False else 'STUDIO'):
        sh.light = light
        sh.background_color = bg
        cam.data.lens = lens
        cam.location = U(eye)
        cam.rotation_euler = (U(look) - U(eye)).to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = os.path.join(out_dir, name + '.png')
        bpy.ops.render.render(write_still=True)
    shot('a_side', (9.5, 1.9, .3), (0, 1.95, .3), 50)
    shot('b_front', (0, 1.7, 9.5), (0, 1.95, .3), 50)
    shot('c_tq', (5.2, 1.35, 6.4), (0, 2.0, .1), 50)
    shot('d_back', (-3.4, 2.6, -7.8), (0, 2.1, -.2), 50)
    shot('e_game', (2.0, 1.5, 13.5), (0, 1.9, 0), 42)
    shot('f_head', (.8, 2.5, 3.4), (0, 3.25, 1.1), 55)
    shot('g_chest', (1.6, 2.5, 1.9), (0, 3.05, -.15), 45)
    shot('h_hand', (2.4, 1.0, 3.0), (.92, .3, .9), 55)
    shot('i_spine', (-2.6, 3.0, -3.0), (0, 3.0, -.5), 45)
    shot('j_foot', (1.8, .8, 1.2), (.34, .35, -.6), 55)
    shot('m_headback', (1.5, 3.7, -.5), (0, 3.3, 1.0), 45)
    shot('n_headside', (3.6, 3.1, 1.25), (0, 3.3, 1.1), 40)
    shot('o_wrist', (-1.9, 1.3, 2.2), (-.92, .5, .8), 40)
    # The dark: nothing but what's lit.
    for ob in obs:
        if ob.name == 'body':
            ca = ob.data.color_attributes['Col']
            for d in ca.data:
                c = d.color_srgb
                d.color_srgb = (c[0] * .08, c[1] * .08, c[2] * .08, 1)
    for d in a.data:
        d.color_srgb = (.03, .03, .025, 1)
    scene.view_settings.exposure = 0
    shot('k_dark_side', (9.5, 1.9, .3), (0, 1.95, .3), 50, bg=(.0, .0, .0), light='FLAT')
    shot('l_dark_front', (1.5, 1.7, 9.5), (0, 1.95, .3), 50, bg=(.0, .0, .0), light='FLAT')


# ===========================================================================================================================
# main
# ===========================================================================================================================
def print_table():
    print('// Generated by tools/gear/hum_boss.py --table: name, parent, bind position (boss axes).')
    for i, (name, parent, p) in enumerate(JOINTS):
        print('J(%d,"%s",%d,%sf,%sf,%sf);' % (i, name, INDEX[parent] if parent else -1, fmt(p.x), fmt(p.y), fmt(p.z)))
    print('const count', len(JOINTS))


def fmt(x):
    return ('%.4f' % x).rstrip('0').rstrip('.') if abs(x) > 1e-5 else '0'


def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument('--out', default=None)
    ap.add_argument('--tiers', default='0,1,2')
    ap.add_argument('--sheet', default=None)
    ap.add_argument('--sheet-tier', type=int, default=0)
    ap.add_argument('--table', action='store_true')
    a = ap.parse_args(argv)
    if a.table:
        print_table()
        return
    make_finishes()
    bank = gk.GearBank()
    baked = None
    totals = {}
    for tier in [int(x) for x in a.tiers.split(',')]:
        make_finishes()
        solids, glows = build_tier(tier)
        out, tris = write(bank, tier, solids, glows)
        totals[tier] = tris
        if tier == a.sheet_tier:
            baked = out
        if tris > BUDGET[tier] * 1.08:
            print('WARNING: tier %d is over its budget: %d' % (tier, tris))
    if a.out:
        bank.write(a.out, BANK)
    if a.sheet and baked is not None:
        sheet(baked, a.sheet, a.sheet_tier)
    print('totals', totals)


if __name__ == '__main__':
    main()
