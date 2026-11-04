"""THE 6:47, the Central Station's boss, modelled in Blender (docs/EXPEDITION-ENEMY-REWORK.md section 2: "the bosses' machines ...
rebuilt in Blender as detailed hard-surface parts on today's joints, so their rigs, motion library, colliders and rules stay as they
are"; docs/EXPEDITION-BOSS-CENTRAL-STATION.md for the design). It writes the bank Resources/ChipArenaPackBoss647 (gearkit.py's format
and bake): one prop per joint and tier, "647-<joint>-<tier>", each mesh in its joint's own frame (rest rotations are identity, so a
frame is an offset), the joint's name its group. Runtime/NativeStationBossBody.cs draws them on Core StationBossRig's pose.

usage: blender --background --factory-startup --python tools/gear/station_boss.py -- --out ASSETS/LocalAssets [--preview DIR] [--only joint,...] [--tiers 0,1,2] [--sheet DIR]

Everything here is authored in the boss's own axes (+x its right, +y up, +z the nose; `U` takes them to Blender's) and every number
that has to agree with the game is a joint's rest, a limb's length or a hit volume from Core NativeStationBossModel and
NativeStationBossLook; Tests/StationBossBodyChecks holds this table to them. Nothing here is read by a rule: the colliders stay
Runtime NativeZoneBossAuthority's.

The look: a 1950s evacuation special in station green with a cream band and brass lining, its panels riveted and dented from the
inside by what it carries; the yellow warning nose with its plough for a jaw, a cracked windscreen, the blind reading 6.47 EVAC; six
legs that are bundles of the passengers' own arms in their torn coat sleeves, stopped wristwatches on them, hands for feet.
"""
import argparse
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector, noise

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gearkit as gk  # noqa: E402
from gearkit import Piece, finish  # noqa: E402

BANK = 'ChipArenaPackBoss647'
# The design frame (Blender: X right, Y forward, Z up) to a joint's frame (Unity's axes: x right, y up, z the nose).
TO_JOINT = Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))


def U(x, y=None, z=None):
    """The boss's axes to Blender's."""
    if y is None:
        x, y, z = x
    return Vector((x, z, y))


# ===========================================================================================================================
# The joints (Core NativeStationBossModel, NativeStationBossLook)
# ===========================================================================================================================
BODY_LIFT, RIDER = 1.55, 1.3
HALF_WIDTH, REAR_Z, CAB_Z, NOSE_Z = 1.45, -4.3, 3.05, 4.05
ROW_Z = (3.45, .4, -2.85)
WINDOW_Z = (-3.45, -2.35, .95, 2.05)
LEGS = 6
CHAIN_LINKS, CABLE_LINKS, WINDOW_ARMS, TAIL_LINKS = 4, 3, 4, 3
TAIL_LENGTH = .3
WATCH_PIVOT = (.0066, -.2412, .1586)
PALM_LIFT = .14


def side_of(leg):
    return -1 if leg & 1 == 0 else 1


def row_of(leg):
    return leg >> 1


def thigh(leg):
    return 2.6 if row_of(leg) == 0 else 2.7


def shin(leg):
    return 3.3 if row_of(leg) == 0 else 3.5


def leg_name(leg):
    return ('l' if side_of(leg) < 0 else 'r') + '-' + ('front', 'mid', 'rear')[row_of(leg)]


JOINTS = {}


def _j(name, parent, x, y, z):
    JOINTS[name] = (parent, Vector((x, y, z)))


def _joints():
    r = RIDER
    _j('root', None, 0, 0, 0)
    _j('body', 'root', 0, BODY_LIFT, 0)
    _j('lamp', 'body', 0, 1.0, 5.22)
    _j('heart', 'body', 0, 1.25, -4.05)
    _j('roof', 'body', 0, 2.58, -3.65)
    _j('panto-low', 'roof', 0, .36, 1.05)
    _j('panto-high', 'panto-low', 0, 0, 1.5)
    _j('panto-head', 'panto-high', 0, 0, -1.5)
    _j('sm-hips', 'body', 0, 2.7, 2.35)
    _j('sm-chest', 'sm-hips', 0, .36 * r, 0)
    _j('sm-head', 'sm-chest', 0, .66 * r, .03 * r)
    _j('sm-hat', 'sm-head', 0, .34 * r, .01 * r)
    _j('sm-l-arm', 'sm-chest', -.47 * r, .52 * r, 0)
    _j('sm-l-fore', 'sm-l-arm', 0, -.5 * r, 0)
    _j('sm-l-hand', 'sm-l-fore', 0, -.46 * r, 0)
    _j('sm-r-arm', 'sm-chest', .47 * r, .52 * r, 0)
    _j('sm-r-fore', 'sm-r-arm', 0, -.5 * r, 0)
    _j('sm-r-hand', 'sm-r-fore', 0, -.46 * r, 0)
    for i in range(LEGS):
        s, row = side_of(i), row_of(i)
        y = .5 if row == 0 else .22
        x = 1.58 if row == 0 else 1.66
        n = leg_name(i)
        _j('wheel-' + n, 'body', s * (x - .14), y, ROW_Z[row])
        _j('hip-' + n, 'body', s * x, y, ROW_Z[row])
        _j('knee-' + n, 'hip-' + n, 0, -thigh(i), 0)
        _j('foot-' + n, 'knee-' + n, 0, -shin(i), 0)
    _j('door-l-front', 'body', -1.465, 1.33, -.025)
    _j('door-l-back', 'body', -1.465, 1.33, -.675)
    _j('door-r-front', 'body', 1.465, 1.33, -.025)
    _j('door-r-back', 'body', 1.465, 1.33, -.675)
    _j('grab-l', 'body', -.25, 1.25, -.35)
    _j('grab-l-hands', 'grab-l', -1.15, 0, 0)
    _j('grab-r', 'body', .25, 1.25, -.35)
    _j('grab-r-hands', 'grab-r', 1.15, 0, 0)
    for k in range(CHAIN_LINKS):
        _j('chain-%d' % k, 'body' if k == 0 else 'chain-%d' % (k - 1), 0, .3 if k == 0 else -.34, -4.42 if k == 0 else 0)
    for k in range(CABLE_LINKS):
        _j('cable-a-%d' % k, 'body' if k == 0 else 'cable-a-%d' % (k - 1), -.95 if k == 0 else 0, 2.42 if k == 0 else -.62, -4.36 if k == 0 else 0)
        _j('cable-b-%d' % k, 'body' if k == 0 else 'cable-b-%d' % (k - 1), .78 if k == 0 else 0, 2.5 if k == 0 else -.62, -4.36 if k == 0 else 0)
    arm_z = (-2.35, 2.05, -3.45, .95)
    for a in range(WINDOW_ARMS):
        s = -1 if a < 2 else 1
        _j('window-arm-%d' % a, 'body', s * 1.47, 1.3, arm_z[a])
        _j('window-hand-%d' % a, 'window-arm-%d' % a, 0, -.56, 0)
    _j('horn-l', 'body', -.78, 2.62, 3.35)
    _j('horn-r', 'body', .78, 2.62, 3.35)
    for k in range(TAIL_LINKS):
        _j('tail-l-%d' % k, 'sm-hips' if k == 0 else 'tail-l-%d' % (k - 1), -.17 * r if k == 0 else 0, .12 if k == 0 else -TAIL_LENGTH, -.42 * r if k == 0 else 0)
        _j('tail-r-%d' % k, 'sm-hips' if k == 0 else 'tail-r-%d' % (k - 1), .17 * r if k == 0 else 0, .12 if k == 0 else -TAIL_LENGTH, -.42 * r if k == 0 else 0)
    for sd in range(2):
        for k in range(4):
            _j('window-%s-%d' % ('lr'[sd], k), 'body', (-1 if sd == 0 else 1) * HALF_WIDTH, 1.55, WINDOW_Z[k])
    _j('cab-glow', 'body', 0, 1.45, 4.0)
    _j('markers', 'body', 0, 1.3, 5.25)
    _j('breach-l', 'body', -1.45, 1.2, -.7)
    _j('breach-r', 'body', 1.45, 1.2, -.7)
    # (Core NativeStationBossLook.WatchPivot: the concept's minute hand's end on the right hand.)
    _j('watch-647', 'sm-r-hand', *WATCH_PIVOT)
    _j('watch-648', 'sm-r-hand', *WATCH_PIVOT)


_joints()


def rest(name):
    """A joint's rest in the model's frame."""
    p = Vector((0, 0, 0))
    while name is not None:
        parent, off = JOINTS[name]
        p += off
        name = parent
    return p


# ===========================================================================================================================
# Tiers, finishes, primitives in the boss's axes
# ===========================================================================================================================
class Tier:
    def __init__(self, level):
        self.level = level
        self.fine, self.mid = level == 0, level <= 1

    def s(self, n, low=None):
        """A circle's segments at this tier."""
        return n if self.fine else max(low or 4, int(n * (.66 if self.mid else .4)))


def finishes():
    # The livery: paint chipping to grey primer and rust on its edges.
    finish('green', 0x3A5E50, 'paint', under=0x7A6A54)
    finish('green-dark', 0x28423A, 'paint', under=0x5A4A36)
    finish('cream', 0xB6AA8C, 'paint', under=0x6E6858)
    finish('yellow', 0xD2A41C, 'paint', under=0x5A5448)
    finish('roof', 0x343C42, 'paint', under=0x4A4846, dirt=1.2)
    finish('roof-rib', 0x1C2124, 'plastic', dirt=1.2)
    finish('chevron', 0x0E0F10, 'paint', under=0x5A5448, outline=False)
    finish('line', 0x0A0C0C, 'sticker', outline=False)
    # Metals.
    finish('brass', 0xC8A040, 'metal')
    finish('gold', 0xD4AE48, 'metal')
    finish('iron', 0x17191B, 'metal', dirt=1.2)
    finish('iron-mid', 0x2A2D2F, 'metal', dirt=1.2)
    finish('steel', 0x5E666A, 'metal')
    finish('rust', 0x6A3A22, 'plastic', dirt=1.1)
    finish('copper', 0xC07A3A, 'metal', outline=False)
    finish('rivet', 0x34383A, 'metal', outline=False)
    finish('tread', 0x4E5356, 'metal')
    # Glass, rubber, cloth, leather.
    finish('rubber', 0x0E0F10, 'rubber')
    finish('hose', 0x121316, 'rubber', outline=False)
    finish('insulator', 0xD8D6CC, 'plastic', outline=False)
    finish('dead-glass', 0x1A2428, 'plastic', outline=False)
    finish('shard', 0x2A3438, 'plastic', outline=False)
    finish('frame', 0x101214, 'plastic')
    finish('shadow', 0x0A0807, 'sticker', outline=False)
    finish('lining', 0x14110E, 'cloth')
    finish('black', 0x050607, 'sticker', outline=False)
    finish('leather', 0x3A2416, 'cloth')
    finish('leather-tan', 0x6A4A2A, 'cloth')
    finish('case', 0x4A3A28, 'paint', under=0x2A2016)
    finish('case-blue', 0x24324A, 'paint', under=0x14182A)
    finish('coat', 0x111317, 'cloth')
    finish('coat-dark', 0x08090B, 'cloth')
    finish('hat', 0x0A0B0D, 'plastic')
    for i, h in enumerate((0x1E2636, 0x3A2E24, 0x44474E, 0x55503A, 0x4A1E24, 0x26302A)):
        finish('cloth%d' % i, h, 'cloth')
    # The passengers: drowned grey-green flesh with deep creases.
    finish('flesh', 0x7E8876, 'skin')
    finish('flesh-dark', 0x565E52, 'skin')
    finish('flesh-pale', 0xA0A68E, 'skin')
    finish('nail', 0x2A2622, 'sticker', outline=False)
    finish('bone', 0xB8B09A, 'plastic', outline=False)
    finish('tooth', 0xA8A080, 'plastic', outline=False)
    # What glows (the second sub-mesh; each joint's light is the presentation's).
    # (The toon draws a glowing vertex by its alpha: gearkit's "outline" on.)
    finish('amber', 0xFFB45A, 'glow')
    finish('amber-dim', 0xC4762C, 'glow')
    finish('core', 0xFFD27A, 'glow')
    finish('vein', 0xFF9A3C, 'glow')
    finish('lamp', 0xF2F6FF, 'glow')
    finish('marker', 0xFFE8C0, 'glow')
    finish('live', 0x6FD8FF, 'glow')


# While a limb bakes: its radius at the root and the tip and its length: a point near its axis lies in the creases between the strands.
LIMB = None


def shade(f, p, n, cav, seed):
    """gearkit's bake with the carriage's weather: the finish's wear, then dirt rising from the underframe, soot along the roof,
    rust running down from the window rails, by the design-space point `p` (the joint's own frame: only large parts read far)."""
    c = _shade(f, p, n, cav, seed)
    if f.glow:
        return c
    if LIMB is not None and f.kind == 'skin':
        r0, r1, length = LIMB
        u = max(0.0, min(1.0, -p.z / length))
        r = math.hypot(p.x, p.y) / max(.01, r0 + (r1 - r0) * u)
        c = c * (.34 + .66 * gk._smooth(.42, .86, r))
        # Bruises and old blood along the limb, by the strand.
        bruise = gk._smooth(.12, .5, noise.noise(Vector((p.x * 4.1 + seed, p.y * 4.1, p.z * 1.4))))
        c = c.lerp(Vector((.27, .16, .19)), .4 * bruise)
        return Vector((max(0, min(1, c.x)), max(0, min(1, c.y)), max(0, min(1, c.z))))
    if f.kind in ('sticker', 'skin', 'flesh'):
        return c
    q = Vector(p)
    streak = noise.noise(Vector((q.y * 3.3 + seed, q.x * 3.3, q.z * .35)))
    c = c * (1 - .16 * max(0.0, streak))
    if f.kind == 'paint':
        patch = gk._smooth(.2, .55, noise.noise(q * 2.3 + Vector((seed, 1.5, .7))))
        c = c.lerp(Vector((.24, .15, .1)), .2 * patch)
    return Vector((max(0, min(1, c.x)), max(0, min(1, c.y)), max(0, min(1, c.z))))


_shade = gk.shade
gk.shade = shade


def bx(pc, c, s, fin, axes=None):
    """A box at `c`, sizes `s` along its local axes (x, y, z; default the boss's); `axes` three unit vectors in the boss's axes."""
    if axes is None:
        return gk.box(pc, U(c), (s[0], s[2], s[1]), fin)
    u, v, w = [Vector(a).normalized() for a in axes]
    m = Matrix((U(u), U(w), U(v))).transposed()
    return gk.box(pc, U(c), (s[0], s[2], s[1]), fin, m)


def tilted(rx=0.0, ry=0.0, rz=0.0):
    """Local axes turned by Euler angles (degrees) in the boss's axes."""
    m = Matrix.Rotation(math.radians(ry), 3, 'Y') @ Matrix.Rotation(math.radians(rx), 3, 'X') @ Matrix.Rotation(math.radians(rz), 3, 'Z')
    return (m @ Vector((1, 0, 0)), m @ Vector((0, 1, 0)), m @ Vector((0, 0, 1)))


def cy(pc, a, b, r, fin, segs=12, r1=None, caps=True):
    return gk.cyl(pc, U(a), U(b), r, fin, segs, r1, caps)


def sp(pc, c, r, fin, u=12, v=8):
    if not isinstance(r, (tuple, list)):
        r = (r, r, r)
    return gk.sphere(pc, U(c), (r[0], r[2], r[1]), fin, u, v)


def tu(pc, pts, radius, fin, segs=8, radii=None, closed=False, caps=True):
    return gk.tube(pc, [U(p) for p in pts], radius, fin, segs, closed, caps, radii)


def rg(pc, c, axis, major, minor, fin, segs=16, rsegs=6):
    return gk.torus(pc, U(c), U(axis), major, minor, fin, segs, rsegs)


def cm(pts, per=5):
    return gk.catmull([Vector(p) for p in pts], per)


GLYPHS = {
    '0': '01110 10001 10011 10101 11001 10001 01110', '1': '00100 01100 00100 00100 00100 00100 01110', '4': '00010 00110 01010 10010 11111 00010 00010',
    '6': '00110 01000 10000 11110 10001 10001 01110', '7': '11111 00001 00010 00100 01000 01000 01000', '8': '01110 10001 10001 01110 10001 10001 01110',
    ':': '00000 00100 00100 00000 00100 00100 00000', '.': '00000 00000 00000 00000 00000 00100 00100', ' ': '00000 00000 00000 00000 00000 00000 00000',
    'A': '01110 10001 10001 11111 10001 10001 10001', 'C': '01111 10000 10000 10000 10000 10000 01111', 'E': '11111 10000 10000 11110 10000 10000 11111',
    'I': '11111 00100 00100 00100 00100 00100 11111', 'L': '10000 10000 10000 10000 10000 10000 11111', 'N': '10001 11001 10101 10101 10011 10001 10001',
    'O': '01110 10001 10001 10001 10001 10001 01110', 'P': '11110 10001 10001 11110 10000 10000 10000', 'R': '11110 10001 10001 11110 10100 10010 10001',
    'S': '01111 10000 10000 01110 00001 00001 11110', 'T': '11111 00100 00100 00100 00100 00100 00100', 'U': '10001 10001 10001 10001 10001 10001 01110',
    'V': '10001 10001 10001 10001 01010 01010 00100', 'D': '11110 10001 10001 10001 10001 10001 11110', 'M': '10001 11011 10101 10101 10001 10001 10001',
}


def word(pc, text, o, u, v, n, cell, depth, fin):
    """Block letters on a face: `u` the reading direction, `v` up, `n` out of the face, centred on `o`."""
    o, u, v, n = Vector(o), Vector(u), Vector(v), Vector(n)
    width = len(text) * 6 * cell - cell
    for i, ch in enumerate(text):
        rows = GLYPHS[ch].split()
        for r, row in enumerate(rows):
            for k, bit in enumerate(row):
                if bit == '1':
                    c = o + u * (-width / 2 + (i * 6 + k + .5) * cell) + v * ((3 - r) * cell) + n * (depth * .5)
                    bx(pc, c, (cell * 1.02, cell * 1.02, depth), fin, (u, v, n))


# ===========================================================================================================================
# Skins: lofted rings with a grid fine enough to carry the weather
# ===========================================================================================================================
def loft(pc, rings, fin_of, closed=False, skip=None, push=None):
    """Quads between successive rings (each a list of points in the boss's axes). `fin_of(p)` a quad centre's finish, `skip(p)` leaves
    a hole, `push(p)` moves a point (dents and bulges)."""
    verts = []
    for ring in rings:
        row = []
        for p in ring:
            q = Vector(p)
            if push:
                q = push(q)
            row.append(pc.bm.verts.new(U(q)))
        verts.append(row)
    m = len(rings[0])
    faces = []
    for i in range(len(rings) - 1):
        for k in range(m if closed else m - 1):
            k2 = (k + 1) % m
            centre = (Vector(rings[i][k]) + Vector(rings[i + 1][k]) + Vector(rings[i][k2]) + Vector(rings[i + 1][k2])) / 4
            if skip and skip(centre):
                continue
            f = pc.bm.faces.new((verts[i][k], verts[i + 1][k], verts[i + 1][k2], verts[i][k2]))
            f.material_index = pc.slot(fin_of(centre))
            faces.append(f)
    return faces


def stations(a, b, step, breaks=()):
    """Positions from a to b about `step` apart, every break among them."""
    marks = sorted({round(x, 4) for x in [a, b] + [x for x in breaks if a < x < b]})
    out = []
    for p, q in zip(marks, marks[1:]):
        n = max(1, round((q - p) / step))
        out += [p + (q - p) * i / n for i in range(n)]
    return out + [b]


def bumps(q, list_):
    """Gaussian bulges (centre, radius, height) pushing a wall outward: the bodies inside pressing on it, `q` moved along x."""
    d = 0.0
    for c, r, h in list_:
        e = (q - Vector(c)).length_squared / (r * r)
        if e < 6:
            d += h * math.exp(-e)
    return d


# ---- the carriage's side wall: the section (half width at a height), straight below the window rail, tumbling home above it ----
SECTION = ((.44, 1.385), (.52, 1.425), (.72, 1.45), (.96, 1.46), (1.125, 1.47), (1.15, 1.46), (1.33, 1.455), (1.55, 1.45), (1.77, 1.45), (1.96, 1.445), (1.975, 1.45), (2.12, 1.435), (2.3, 1.405), (2.42, 1.355), (2.47, 1.33))
WINDOW_HALF_Z, WINDOW_Y0, WINDOW_Y1 = .49, 1.17, 1.93
DOOR_Z0, DOOR_Z1, DOOR_Y1 = -1.0, .3, 2.2


def wall_hole(p):
    if DOOR_Z0 < p.z < DOOR_Z1 and p.y < DOOR_Y1:
        return True
    if WINDOW_Y0 < p.y < WINDOW_Y1:
        return any(abs(p.z - z) < WINDOW_HALF_Z for z in WINDOW_Z)
    return False


def wall_finish(p):
    return 'cream' if 1.125 < p.y < 1.975 else 'green'


def carriage(t):
    """The side walls from the torn rear to the cab: riveted panels, the window and door openings, the brass lining, bulges where
    the mass inside presses out, the rust and soot."""
    pc = Piece('walls')
    pc.sharp = 40
    step = .08 if t.fine else .22 if t.mid else .5
    breaks = [DOOR_Z0, DOOR_Z1] + [z + s * WINDOW_HALF_Z for z in WINDOW_Z for s in (-1, 1)]
    zs = stations(REAR_Z, CAB_Z, step, breaks)
    ys = list(SECTION)
    if t.fine or t.mid:
        # A finer section between the given heights where the wall is flat: the weather needs points.
        fine = []
        for (y0, x0), (y1, x1) in zip(ys, ys[1:]):
            n = max(1, round((y1 - y0) / (.07 if t.fine else .2)))
            fine += [(y0 + (y1 - y0) * i / n, x0 + (x1 - x0) * i / n) for i in range(n)]
        ys = fine + [ys[-1]]
    for y_edge in (WINDOW_Y0, WINDOW_Y1, DOOR_Y1):
        best = min(range(len(ys)), key=lambda i: abs(ys[i][0] - y_edge))
        ys[best] = (y_edge, ys[best][1])
    for side in (-1, 1):
        lumps = [((side * 1.47, .86, -3.1), .38, .05), ((side * 1.46, .82, 1.5), .42, .06), ((side * 1.46, 2.1, -1.9), .35, .035), ((side * 1.46, .95, -1.6 if side < 0 else 2.55), .3, .05), ((side * 1.45, 2.2, 2.3), .3, .03)]

        def push(q, side=side, lumps=lumps):
            d = bumps(q, lumps)
            d += .006 * noise.noise(q * 5.0)
            return q + Vector((side * d, 0, 0))
        rings = [[(side * x, y, z) for y, x in ys] for z in zs]
        loft(pc, rings, wall_finish, False, wall_hole, push)
    # The inside: a dark lining a little behind the openings.
    for side in (-1, 1):
        bx(pc, (side * 1.37, 1.44, -.6), (.02, 1.96, 7.3), 'lining')
    pc2 = Piece('wall-detail')
    pc2.sharp = 30
    for side in (-1, 1):
        x = side * HALF_WIDTH
        for y in (1.125, 1.975):
            for z0, z1 in ((REAR_Z, DOOR_Z0), (DOOR_Z1, CAB_Z)):
                bx(pc2, (side * (1.468 if y < 1.5 else 1.452), y, (z0 + z1) / 2), (.016, .028, z1 - z0), 'brass')
        # The rail under the windows, the rain strip over them, the solebar's skirt.
        for z0, z1 in ((REAR_Z, DOOR_Z0), (DOOR_Z1, CAB_Z)):
            bx(pc2, (side * 1.4, 2.46, (z0 + z1) / 2), (.1, .035, z1 - z0), 'iron-mid')
            bx(pc2, (side * 1.43, .46, (z0 + z1) / 2), (.05, .06, z1 - z0), 'iron')
        if t.mid:
            # The panel seams: a raised strap every panel, rivets down both edges.
            seams = [REAR_Z + .35, -2.9, -1.8, -1.06, .36, 1.5, 2.6]
            for z in seams:
                for y0, y1 in ((.5, 1.11), (1.99, 2.42)):
                    h = y1 - y0
                    xw = next(xx for yy, xx in SECTION if yy >= (y0 + y1) / 2)
                    bx(pc2, (side * (xw + .004), (y0 + y1) / 2, z), (.012, h, .05), 'green-dark')
                    if t.fine:
                        n = int(h / .1)
                        for i in range(n):
                            yy = y0 + (i + .5) * h / n
                            for dz in (-.018, .018):
                                cy(pc2, (side * (xw + .006), yy, z + dz), (side * (xw + .018), yy, z + dz), .009, 'rivet', 5)
            if t.fine:
                # The rivet line under the belt and over the windows.
                for z0, z1 in ((REAR_Z + .1, DOOR_Z0 - .1), (DOOR_Z1 + .1, CAB_Z - .1)):
                    n = int((z1 - z0) / .12)
                    for i in range(n):
                        z = z0 + (i + .5) * (z1 - z0) / n
                        cy(pc2, (side * 1.466, 1.07, z), (side * 1.482, 1.07, z), .009, 'rivet', 5)
                        cy(pc2, (side * 1.452, 2.03, z), (side * 1.466, 2.03, z), .009, 'rivet', 5)
                # The number and the name.
                word(pc2, '6:47', (side * 1.478, .84, -1.55 if side < 0 else 1.55), (0, 0, side), (0, 1, 0), (side, 0, 0), .028, .008, 'brass')
                word(pc2, 'EVACUATION', (side * 1.478, .92, 1.9 if side < 0 else -1.9), (0, 0, side), (0, 1, 0), (side, 0, 0), .018, .004, 'cream')
                word(pc2, 'SPECIAL', (side * 1.478, .76, 1.9 if side < 0 else -1.9), (0, 0, side), (0, 1, 0), (side, 0, 0), .018, .004, 'cream')
        # Door frame posts and the sill.
        for z in (DOOR_Z0 - .03, DOOR_Z1 + .03):
            bx(pc2, (side * 1.46, 1.33, z), (.04, 1.8, .07), 'iron-mid')
        bx(pc2, (side * 1.45, .47, (DOOR_Z0 + DOOR_Z1) / 2), (.08, .05, DOOR_Z1 - DOOR_Z0 + .1), 'steel')
        bx(pc2, (side * 1.45, 2.23, (DOOR_Z0 + DOOR_Z1) / 2), (.08, .06, DOOR_Z1 - DOOR_Z0 + .1), 'iron-mid')
        if t.mid:
            # Grab rails beside the doors, a flesh growth burst through the seam, torn lips where a panel gave.
            for z in (DOOR_Z0 - .09, DOOR_Z1 + .09):
                tu(pc2, [(side * 1.49, 1.0, z), (side * 1.52, 1.05, z), (side * 1.52, 1.75, z), (side * 1.49, 1.8, z)], .014, 'brass', t.s(6))
            bz = 1.55 if side < 0 else -2.95
            for k in range(5):
                a = k * 1.3
                u = tilted(ry=side * (15 + 8 * math.sin(a)), rx=25 * math.sin(a * 2), rz=side * 30 * math.cos(a))
                bx(pc2, (side * 1.485, .78 + .3 * math.cos(a) * .8, bz + .34 * math.sin(a)), (.05, .22, .12), 'green-dark', u)
            bx(pc2, (side * 1.43, .78, bz), (.04, .5, .62), 'shadow')
            sp(pc2, (side * 1.46, .78, bz), (.1, .22, .26), 'flesh-dark', t.s(10), t.s(6))
            cy(pc2, (side * 1.5, .62, bz - .1), (side * 1.52, .96, bz + .1), .012, 'vein', 4)
    pc.bevel(.0)
    return [pc.make(False), pc2.make()]


# ---- the cab and the nose: one loft from the cab's rear to the warning front, rounded at the corners ----
def rounded_section(w, y0, y1, r, n):
    """A closed loop of points in x, y: a rectangle of half width w from y0 to y1, its corners rounded by r."""
    pts = []
    corners = ((w - r, y1 - r, 0), (-(w - r), y1 - r, 90), (-(w - r), y0 + r, 180), (w - r, y0 + r, 270))
    for cx, cy_, a0 in corners:
        for i in range(n + 1):
            a = math.radians(a0 + 90 * i / n)
            pts.append((cx + r * math.cos(a), cy_ + r * math.sin(a)))
    return pts


def nose_profile(z):
    """Half width, bottom and top of the cab at z."""
    u = max(0.0, (z - NOSE_Z) / (5.2 - NOSE_Z))
    w = 1.45 - .22 * u * u
    y0 = .44 - .02 * u
    # The windscreen rakes back from y 1.55 at z 5.05 up to the roof at z 4.3.
    top = 2.62 if z < 4.3 else 2.62 - (z - 4.3) / .85 * 1.1
    top = max(top, 1.3)
    return w, y0, top


def cab(t):
    pc = Piece('cab')
    pc.sharp = 35
    zs = stations(CAB_Z, 5.2, .07 if t.fine else .18 if t.mid else .4, (NOSE_Z, 4.3, 4.72))
    n = 4 if t.fine else 2

    def fin(p):
        if p.y < 1.5 and p.z > NOSE_Z + .1:
            return 'yellow'
        if 1.125 < p.y < 1.975 and abs(p.x) > 1.3 and p.z < 4.1:
            return 'cream'
        return 'green'
    rings = []
    for z in zs:
        w, y0, y1 = nose_profile(z)
        rings.append([(x, y, z) for x, y in rounded_section(w, y0, y1, min(.2, (y1 - y0) * .3), n)])

    def push(q):
        return q + Vector((0, 0, .004 * noise.noise(q * 6.0)))
    loft(pc, rings, fin, True, None, push)
    # The warning front's cap (the face, slightly domed).
    w, y0, y1 = nose_profile(5.2)
    bx(pc, (0, (y0 + y1) / 2, 5.205), (2 * w - .04, y1 - y0 - .02, .03), 'yellow')
    d = Piece('nose-detail')
    d.sharp = 30
    if t.mid:
        # The worn chevrons on the warning panel: alternate black bars raked toward the buffers.
        for side in (-1, 1):
            for k in range(3 if t.fine else 2):
                bx(d, (side * (1.02 - k * .34), .78, 5.226), (.13, .72, .012), 'chevron', tilted(rz=side * 35))
    # The window frame round the raked screen, the two panes, the wiper arms (their lit panes are the cab-glow joint).
    screen = Vector((0, 1.86, 4.82))
    up = Vector((0, .6, -.45)).normalized()
    out = Vector((0, .45, .6)).normalized()
    across = Vector((1, 0, 0))
    bx(d, screen, (2.46, 1.0, .03), 'frame', (across, up, out))
    bx(d, screen + across * 0 + out * .02, (.09, 1.0, .03), 'green-dark', (across, up, out))
    if t.mid:
        for s in (-1, 1):
            bx(d, screen + across * s * .56 - up * .2 + out * .05, (.5, .02, .012), 'iron', tilted(rz=s * 25))
    bx(d, screen + up * .59 + out * .04, (2.2, .15, .05), 'green-dark', (across, up, out))
    # The destination box's surround, its bolts.
    bx(d, screen + up * .35 + out * .03, (2.04, .24, .03), 'black', (across, up, out))
    # Brass grab handles up the corners, the hand rail, a ladder step.
    if t.mid:
        for s in (-1, 1):
            tu(d, [(s * 1.27, 1.0, 5.14), (s * 1.3, 1.04, 5.2), (s * 1.3, 1.5, 5.12), (s * 1.27, 1.54, 5.05)], .018, 'brass', t.s(6))
            bx(d, (s * 1.2, .47, 5.15), (.24, .03, .18), 'steel')
            word(d, '6:47', (s * 1.466, .86, 3.55), (0, 0, s), (0, 1, 0), (s, 0, 0), .04, .012, 'brass')
    # Cab side frames (their lit panes are cab-glow).
    for s in (-1, 1):
        bx(d, (s * 1.456, 1.75, 3.55), (.02, .6, .62), 'frame')
    # The buffers on their beam, the screw coupling, the plough: an iron jaw with its worn hazard stripes and the things it
    # scooped up on the way (a suitcase crushed into it).
    bx(d, (0, .62, 5.26), (2.6, .34, .12), 'iron')
    if t.fine:
        for x in (-1.1, -.55, 0, .55, 1.1):
            for y in (.5, .74):
                cy(d, (x, y, 5.32), (x, y, 5.345), .022, 'rivet', 6)
    for s in (-1, 1):
        cy(d, (s * .98, .62, 5.28), (s * .98, .62, 5.5), .11, 'steel', t.s(12), .1)
        cy(d, (s * .98, .62, 5.3), (s * .98, .62, 5.38), .15, 'iron-mid', t.s(12))
        cy(d, (s * .98, .62, 5.48), (s * .98, .62, 5.58), .25, 'iron', t.s(20), .24)
        if t.mid:
            rg(d, (s * .98, .62, 5.58), (0, 0, 1), .2, .018, 'steel', t.s(20), 4)
    bx(d, (0, .5, 5.36), (.32, .22, .24), 'iron')
    if t.mid:
        # The screw coupling hanging from its hook.
        cy(d, (0, .5, 5.48), (0, .45, 5.6), .035, 'iron-mid', 6)
        rg(d, (0, .42, 5.64), (1, 0, 0), .06, .016, 'iron-mid', t.s(10), 4)
    for s in (-1, 1):
        ca, sa = math.cos(math.radians(24)), math.sin(math.radians(24))
        along = Vector((s * ca, 0, -sa))
        out = Vector((s * sa, 0, ca))
        tip = Vector((0, 0, 5.5))
        # The blade: raked back from the point at the middle, leaning over its foot; its foot's cutting edge worn bright.
        lean = Vector((0, 1, -.28)).normalized()
        c = tip + along * .82 + lean * .02
        bx(d, c, (1.64, .62, .07), 'iron', (along, lean, along.cross(lean) * s))
        bx(d, c - lean * .32 + out * .02, (1.66, .05, .05), 'steel', (along, lean, along.cross(lean) * s))
        if t.mid:
            for k in range(4):
                u = .25 + k * .4
                fin_ = 'yellow' if k % 2 == 0 else 'chevron'
                bx(d, tip + along * u + lean * .1 + out * .04, (.34, .42, .01), fin_, (along, lean, along.cross(lean) * s))
            for k in range(3):
                u = .45 + k * .5
                # The ribs behind it, from the blade back to the frame.
                bx(d, tip + along * u - out * .22, (.04, .4, .42), 'iron-mid', (along, lean, out))
        if t.fine:
            for k in range(9):
                u = .1 + k * .18
                cy(d, tip + along * u + lean * .26 + out * .036, tip + along * u + lean * .26 + out * .05, .014, 'rivet', 6)
            # Bars of the jaw along the foot, bent like teeth.
            for k in range(8):
                u = .08 + k * .2
                bx(d, tip + along * u - lean * .35, (.05, .11 + .04 * (k % 2), .05), 'steel', tilted(rx=-18 + 12 * (k % 3), rz=s * (k * 7 - 10)))
    bx(d, (0, -.24, 5.0), (2.4, .08, .8), 'iron')
    if t.mid:
        # The suitcase crushed into the plough, its strap trailing.
        bx(d, (-.62, .08, 5.34), (.5, .34, .13), 'case', tilted(ry=26, rz=12))
        bx(d, (-.62, .08, 5.41), (.42, .03, .012), 'leather-tan', tilted(ry=26, rz=12))
        if t.fine:
            tu(d, cm([(-.42, .0, 5.34), (-.3, -.2, 5.36), (-.34, -.42, 5.3)]), .012, 'leather', 4)
    # The front roof, its cant rails, ribs and soot, and the twin brass air horns (the PA horns are their own joints).
    roof_arch(d, 1.45, 3.85, t, fixed=True)
    if t.mid:
        for s in (-1, 1):
            cy(d, (s * .22, 2.84, 3.82), (s * .22, 2.84, 4.15), .045, 'brass', t.s(8))
            cy(d, (s * .22, 2.84, 4.12), (s * .22, 2.84, 4.2), .05, 'brass', t.s(10), .09)
            cy(d, (s * .78, 2.72, 2.9), (s * .78, 2.74, 3.32), .03, 'hose', t.s(5))
    return [pc.make(False), d.make()]


def roof_arch(pc, z0, z1, t, fixed=False, y_off=0.0, z_off=0.0):
    """The arched roof between z0 and z1 (joint offset y_off, z_off taken off), ribbed, a rain strip along each side."""
    zs = stations(z0, z1, .1 if t.fine else .3 if t.mid else 1.0)
    n = 16 if t.fine else 8 if t.mid else 5
    rings = []
    for z in zs:
        ring = []
        for i in range(n + 1):
            a = math.pi * i / n
            x = 1.36 * math.cos(a)
            y = 2.45 + .3 * math.sin(a) ** .8
            ring.append((x, y + y_off, z + z_off))
        rings.append(ring)

    def push(q):
        return q + Vector((0, .006 * noise.noise(q * 4.0), 0))
    loft(pc, rings, lambda p: 'roof', False, None, push)
    if t.mid:
        for k in range(int((z1 - z0) / .55)):
            z = z0 + .3 + k * .55
            pts = [(1.37 * math.cos(math.pi * i / 8), 2.46 + .305 * math.sin(math.pi * i / 8) ** .8 + y_off, z + z_off) for i in range(9)]
            tu(pc, pts, .012, 'roof-rib', t.s(4))
    for s in (-1, 1):
        bx(pc, (s * 1.37, 2.46 + y_off, (z0 + z1) / 2 + z_off), (.06, .03, z1 - z0), 'iron')


def underframe(t):
    """The solebars, the floor, queen-post truss rods, battery boxes, brake cylinders and hoses, the bogies' side frames and leaf
    springs at each row, the luggage lashed under it and the flesh hanging from it."""
    pc = Piece('underframe')
    pc.sharp = 35
    for s in (-1, 1):
        bx(pc, (s * 1.37, .2, -.15), (.08, .4, 8.3), 'iron')
        if t.fine:
            for k in range(40):
                z = -4.2 + k * .21
                cy(pc, (s * 1.41, .34, z), (s * 1.42, .34, z), .012, 'rivet', 5)
                cy(pc, (s * 1.41, .06, z), (s * 1.42, .06, z), .012, 'rivet', 5)
    bx(pc, (0, .43, -.13), (2.78, .06, 8.3), 'iron')
    bx(pc, (0, -.16, -1.9), (1.8, .42, 1.5), 'iron-mid')
    bx(pc, (0, -.14, 1.55), (1.5, .38, 1.1), 'iron-mid')
    if t.mid:
        for z0, z1 in ((-2.6, -1.2), (1.05, 2.05)):
            for k in range(5):
                z = z0 + (k + .5) * (z1 - z0) / 5
                bx(pc, (0, -.37, z), (1.2, .02, .06), 'rust')
            for s in (-1, 1):
                bx(pc, (s * .92, -.16, (z0 + z1) / 2), (.03, .3, z1 - z0 - .2), 'iron')
        for s in (-1, 1):
            # The truss rod sagging between its queen posts; the brake cylinder; the air pipe with its hoses.
            tu(pc, [(s * .95, .36, -3.8), (s * .95, -.18, -2.1), (s * .95, -.18, 1.6), (s * .95, .36, 2.9)], .022, 'iron-mid', t.s(6))
            for z in (-2.1, 1.6):
                bx(pc, (s * .95, .08, z), (.05, .5, .05), 'iron')
            cy(pc, (s * .6, -.05, -.95), (s * .6, -.05, -.2), .13, 'iron-mid', t.s(12))
            cy(pc, (s * .6, -.05, -.2), (s * .6, -.05, .3), .03, 'steel', 6)
            cy(pc, (s * 1.18, .1, -4.0), (s * 1.18, .1, 2.9), .035, 'rust', t.s(6))
            if t.fine:
                for z in (-3.4, -1.5, .55, 2.5):
                    tu(pc, cm([(s * 1.18, .1, z), (s * 1.26, -.1, z + .06), (s * 1.3, -.3, z + .02), (s * 1.26, -.42, z - .05)]), .02, 'hose', 5)
    # The bogies: a pressed frame and a leaf spring across each wheel, the axle box behind the wheel.
    for row, z in enumerate(ROW_Z):
        y = .5 if row == 0 else .22
        for s in (-1, 1):
            xf = 1.34 if row == 0 else 1.4
            bx(pc, (s * xf, y, z), (.1, .24, 1.3), 'iron')
            if t.mid:
                for k in range(5):
                    bx(pc, (s * (xf + .03), y + .22 - k * .035, z), (.06, .03, 1.0 - k * .17), 'iron-mid')
                bx(pc, (s * (xf + .03), y + .3, z), (.08, .1, .14), 'iron')
                for dz in (-.5, .5):
                    cy(pc, (s * (xf + .03), y + .24, z + dz), (s * (xf + .03), y + .45, z + dz), .03, 'steel', 6)
    if t.mid:
        # Luggage lashed under the floor: a trunk and a suitcase in their straps, a pram's wheel: what they brought with them.
        bx(pc, (.5, -.25, .55), (.7, .3, .42), 'case', tilted(ry=14, rz=-6))
        bx(pc, (.5, -.09, .55), (.74, .03, .05), 'leather', tilted(ry=14, rz=-6))
        bx(pc, (-.55, -.28, -.5), (.5, .26, .36), 'case-blue', tilted(ry=-22, rz=8))
        for x0, z0 in ((.2, .55), (.8, .55), (-.7, -.5), (-.4, -.5)):
            bx(pc, (x0, -.02, z0), (.03, .42, .05), 'leather')
        # Flesh sagging out of the floor between the frames.
        fl = Piece('under-flesh')
        for c, r in (((-.3, -.02, -3.1), (.5, .16, .42)), ((.42, .0, -.2), (.4, .15, .5)), ((-.5, .0, 2.65), (.34, .13, .3))):
            sp(fl, c, r, 'flesh-dark', t.s(12), t.s(7))
        for x, z in ((-.2, -3.0), (.5, -.1)):
            sp(fl, (x, -.2, z), (.1, .13, .1), 'flesh', t.s(8), t.s(5))
        return [pc.make(), fl.make()]
    return [pc.make()]


def inside(t):
    """The mass inside: bodies heaped from the floor to the roof, seen through the doors, the broken windows and the open roof, lit
    from within."""
    if not t.mid:
        return []
    pc = Piece('mass')
    pc.sharp = 80
    sp(pc, (0, 1.25, -2.6), (1.1, .75, 1.0), 'flesh-dark', t.s(18), t.s(10))
    sp(pc, (.2, 1.3, -1.1), (1.0, .7, .8), 'flesh', t.s(18), t.s(10))
    sp(pc, (-.1, 1.2, .4), (1.05, .7, .9), 'flesh-dark', t.s(18), t.s(10))
    sp(pc, (.1, 1.25, 1.9), (1.05, .68, .85), 'flesh-dark', t.s(16), t.s(9))
    for c, r, fin_ in (((.3, 1.85, -2.0), (.6, .03, .5), 'amber'), ((-.4, 1.8, -.3), (.5, .03, .6), 'amber'), ((0, 1.75, -1.4), (.4, .03, .4), 'core')):
        sp(pc, c, r, fin_, t.s(10), t.s(5))
    rng = random.Random(647)
    for k in range(14 if t.fine else 5):
        x, z = -.9 + rng.random() * 1.8, -3.4 + rng.random() * 4.6
        y = 1.86 + rng.random() * .22
        fin_ = 'flesh-pale' if k % 3 == 0 else 'flesh'
        sp(pc, (x, y, z), (.11, .14, .12), fin_, t.s(8), t.s(5))
        if t.fine:
            # A face at the roof line: its dark sockets and its open mouth toward the opening.
            for s in (-1, 1):
                sp(pc, (x + s * .04, y + .04, z + .1), (.022, .022, .02), 'black', 5, 4)
            sp(pc, (x, y - .05, z + .105), (.035, .025, .012), 'black', 6, 4)
        if k % 2 == 1:
            a = rng.random() * 6.28
            tu(pc, cm([(x, y, z), (x + .12 * math.cos(a), y + .24, z + .12 * math.sin(a)), (x + .18 * math.cos(a), y + .46, z + .18 * math.sin(a))], 3), .035, 'flesh', t.s(6))
    return [pc.make()]


def rear_end(t):
    """The torn rear: the skin ripped away and bent outward in jagged lips round the opening, the ribs of the frame, cables hanging, the
    drawgear, the fixed strip of roof."""
    pc = Piece('rear')
    pc.sharp = 25
    rng = random.Random(4)
    roof_arch(pc, REAR_Z, -3.65, t, fixed=True)
    n = 12 if t.fine else 6 if t.mid else 3
    for s in (-1, 1):
        for k in range(n):
            y = .55 + k * (2.0 / n) + rng.random() * .08
            w, h = .22 + rng.random() * .22, .2 + rng.random() * .16
            a = (rng.random() - .5) * 50
            fin_ = 'cream' if 1.15 < y < 1.95 else 'green'
            bx(pc, (s * (1.32 + rng.random() * .08), y, REAR_Z - .03 - rng.random() * .1), (w, h, .025), fin_, tilted(rx=(rng.random() - .5) * 60, ry=s * (20 + rng.random() * 50), rz=a))
    for k in range(n):
        x = -1.2 + k * 2.4 / max(1, n - 1)
        bx(pc, (x, 2.5 + rng.random() * .1, REAR_Z - .05 - rng.random() * .12), (.3, .1, .2), 'roof', tilted(rx=20 + rng.random() * 35, ry=(rng.random() - .5) * 30))
    # The floor's torn edge, the drawbar and the broken gangway bellows.
    bx(pc, (0, .3, -4.55), (2.4, .06, .5), 'iron', tilted(rx=35))
    if t.mid:
        for s in (-1, 1):
            for k in range(3):
                bx(pc, (s * 1.15, 1.0 + k * .45, -4.42), (.2, .3, .18), 'rubber', tilted(ry=s * 15, rx=k * 9))
            # A rib of the frame standing proud of the tear, the wires out of the conduit.
            cy(pc, (s * 1.3, .5, -4.32), (s * 1.24, 2.4, -4.38), .035, 'iron-mid', t.s(6))
        cy(pc, (0, .3, -4.45), (0, .3, -4.8), .07, 'iron-mid', t.s(8))
        cy(pc, (-.9, 1.6, -4.45), (-.84, 2.4, -4.5), .018, 'brass', 5)
    if t.fine:
        for k in range(6):
            x = -.9 + k * .36
            tu(pc, cm([(x, 2.42, -4.34), (x + .05, 2.05 - k * .05, -4.42), (x - .03, 1.72 - (k % 3) * .14, -4.36)]), .009, 'hose', 4)
    return [pc.make()]


def body_parts(t):
    return carriage(t) + cab(t) + underframe(t) + inside(t) + rear_end(t)


# ===========================================================================================================================
# The roof that peels, its pantograph, the PA horns
# ===========================================================================================================================
def roof_panel(t):
    y0, z0 = -2.58, 3.65
    pc = Piece('roof')
    pc.sharp = 35
    roof_arch(pc, -3.65, 1.45, t, y_off=y0, z_off=z0)
    # The torpedo vents, the arms reaching up out of them, the pantograph's base on its insulators, a plank walkway.
    d = Piece('roof-detail')
    d.sharp = 30
    vents = (.5, 3.0, 4.4)
    for z in vents:
        cy(d, (0, .14, z), (0, .26, z), .15, 'iron', t.s(10), .18)
        if t.mid:
            cy(d, (0, .27, z), (0, .31, z), .24, 'roof-rib', t.s(10))
    if t.mid:
        for k in range(3):
            x = .62 if k == 1 else -.55 + k * .1
            z = vents[k]
            lean = -.3 if k == 1 else .24
            elbow = (x + lean * .2, .55, z)
            wrist = (x + lean * .55, .92, z + .04)
            cl = 'cloth%d' % ((k * 2 + 2) % 6)
            bx(d, (x, .16, z), (.34, .08, .34), 'roof-rib')
            tu(d, [(x, .1, z), elbow, wrist], 0, 'flesh', t.s(7), radii=[.085, .07, .05])
            tu(d, [(x, .12, z), (x + lean * .12, .4, z)], 0, cl, t.s(7), radii=[.11, .095])
            hand(d, wrist, Vector((lean * .5, 1, .1)).normalized(), Vector((0, 0, 1)), .13, 'flesh', t)
    if t.mid:
        for x in (-.42, .42):
            for z in (.75, 1.35):
                cy(d, (x, .14, z), (x, .3, z), .065, 'insulator', t.s(8))
                if t.fine:
                    for k in range(3):
                        cy(d, (x, .17 + k * .05, z), (x, .19 + k * .05, z), .085, 'insulator', 8)
    bx(d, (0, .32, 1.05), (1.0, .05, .74), 'iron')
    if t.fine:
        # The walkway's slats and the conduit to the cab.
        for k in range(12):
            bx(d, (.92, .13, .4 + k * .36), (.3, .02, .3), 'rust', tilted(rz=12))
        tu(d, [(-.3, .3, 1.3), (-.45, .22, 2.0), (-.45, .2, 4.6), (-.3, .22, 5.0)], .03, 'hose', 6)
    return [pc.make(False), d.make()]


def pantograph(which, t):
    pc = Piece('panto')
    pc.sharp = 40
    if which == 'panto-low':
        for s in (-1, 1):
            cy(pc, (s * .3, 0, .02), (s * .22, 0, 1.46), .03, 'iron-mid', t.s(8), .022)
        cy(pc, (-.32, 0, 1.46), (.32, 0, 1.46), .03, 'iron', t.s(8))
        bx(pc, (0, 0, .06), (.7, .08, .12), 'iron')
        cy(pc, (-.36, 0, .02), (.36, 0, .02), .06, 'iron-mid', t.s(10))
        if t.mid:
            # The raising spring and its rod.
            cy(pc, (.18, .03, .1), (.18, .05, .55), .03, 'steel', t.s(6))
            for k in range(7 if t.fine else 0):
                rg(pc, (.18, .03 + k * .003, .12 + k * .06), (0, 0, 1), .04, .007, 'steel', 8, 3)
    elif which == 'panto-high':
        cy(pc, (0, 0, 0), (-.14, 0, -1.46), .03, 'iron-mid', t.s(8), .02)
        cy(pc, (0, 0, 0), (.14, 0, -1.46), .03, 'iron-mid', t.s(8), .02)
        if t.mid:
            bx(pc, (0, 0, 0), (.14, .12, .14), 'iron')
            cy(pc, (-.1, 0, -.75), (.1, 0, -.75), .014, 'iron-mid', 5)
    else:
        bx(pc, (0, .06, 0), (1.7, .06, .12), 'iron')
        bx(pc, (0, .105, 0), (1.56, .03, .09), 'copper')
        for s in (-1, 1):
            bx(pc, (s * .92, 0, 0), (.26, .05, .1), 'iron', tilted(rz=s * -38))
            if t.mid:
                sp(pc, (s * .42, 0, 0), (.04, .05, .04), 'live', t.s(8), t.s(5))
                cy(pc, (s * .42, -.05, 0), (s * .42, .05, 0), .022, 'insulator', 6)
        cy(pc, (0, -.08, 0), (0, .04, 0), .03, 'iron', t.s(6))
    return [pc.make()]


def horn(side, t):
    """A station PA horn grafted to the cab on a sprung stalk: a flared steel bell on its bracket, its cable (the Voice of the Station)."""
    pc = Piece('horn')
    pc.sharp = 40
    s = t.s(16)
    y, z = .38, .2
    cy(pc, (0, .0, -.12), (0, y, z - .22), .02, 'iron', t.s(6))
    cy(pc, (0, y, z - .22), (0, y, z - .06), .07, 'iron-mid', s)
    cy(pc, (0, y, z - .06), (0, y, z + .02), .09, 'steel', s, .14)
    cy(pc, (0, y, z + .02), (0, y, z + .22), .14, 'steel', s, .27)
    cy(pc, (0, y, z + .19), (0, y, z + .2), .25, 'black', s)
    if t.mid:
        rg(pc, (0, y, z + .22), (0, 0, 1), .27, .012, 'iron-mid', s, 4)
        bx(pc, (0, y - .16, z - .12), (.06, .22, .05), 'iron')
    return [pc.make()]


# ===========================================================================================================================
# Hands, arms: the passengers
# ===========================================================================================================================
def hand(pc, wrist, along, palm_out, size, fin, t, curl=.5, spread=.32, thumb=True):
    """A hand from the wrist along `along`, its back toward `palm_out`'s opposite (the palm faces palm_out), `size` the palm's length."""
    w, a, n = Vector(wrist), Vector(along).normalized(), Vector(palm_out).normalized()
    side = a.cross(n).normalized()
    n = side.cross(a).normalized()
    palm_w, palm_l, palm_t = size * .9, size, size * .34
    centre = w + a * (palm_l * .5)
    bx(pc, centre, (palm_w, palm_l, palm_t), fin, (side, a, side.cross(a)))
    fingers = 4 if t.mid else 2
    for k in range(fingers):
        x = ((k + .5) / fingers - .5) * palm_w * .95
        fan = ((k + .5) / fingers - .5) * spread
        d = (a + side * fan).normalized()
        L = size * (.86 if k in (1, 2) else .72) * (1 if t.mid else 1.1)
        base = w + a * palm_l * .96 + side * x
        p1 = base + d * L * .42 + n * L * .42 * curl * .4
        d2 = (d + n * curl).normalized()
        p2 = p1 + d2 * L * .32
        d3 = (d2 + n * curl).normalized()
        p3 = p2 + d3 * L * .26
        r = size * .1 * (1 if t.mid else 1.4)
        tu(pc, [base, p1, p2, p3], 0, fin, t.s(6, 3), radii=[r, r * .92, r * .82, r * .66])
        if t.fine:
            sp(pc, p3 + d3 * r * .1 - n * r * .3, (r * .8, r * .8, r * .8), 'nail', 5, 3)
    if thumb and t.mid:
        b = w + a * palm_l * .25 - side * palm_w * .5
        p1 = b + (a - side * .9).normalized() * size * .4
        p2 = p1 + (a - side * .3 + n * .4).normalized() * size * .36
        r = size * .12
        tu(pc, [b, p1, p2], 0, fin, t.s(6, 3), radii=[r * 1.1, r, r * .75])


def strand(root, tip, twist, phase, offset, lumps, n, seed):
    """A limb's strand: a line from `root` to `tip` wound about the axis, swelling and thinning like a forearm, with its own lumps."""
    root, tip = Vector(root), Vector(tip)
    axis = (tip - root)
    L = axis.length
    d = axis / L
    u = d.cross(Vector((0.3, 0.2, 1))).normalized()
    v = d.cross(u)
    pts, radii = [], []
    for i in range(n + 1):
        s = i / n
        a = phase + twist * s
        c = root + axis * s + (u * math.cos(a) + v * math.sin(a)) * offset(s)
        pts.append(c)
        radii.append(max(.01, lumps(s) * (1 + .12 * noise.noise(Vector((s * 7.3 + seed, seed * 1.7, .3))))))
    return pts, radii


def limb_bundle(pc, root, tip, r0, r1, t, seed, strands=6, fin_a='flesh', fin_b='flesh-dark'):
    """A limb made of arms: a core and `strands` strands wound round it, each thick at one end and thin at the other."""
    rng = random.Random(seed)
    root, tip = Vector(root), Vector(tip)
    L = (tip - root).length
    n = 12 if t.fine else 7 if t.mid else 4
    d = (tip - root) / L
    # The core, its section the limb's taper.
    pts = [root + (tip - root) * (i / n) for i in range(n + 1)]
    tu(pc, pts, 0, fin_b, t.s(10), radii=[(r0 + (r1 - r0) * (i / n)) * (.62 if t.fine else .84) for i in range(n + 1)])
    count = strands if t.fine else max(3, strands - 2) if t.mid else 3
    for k in range(count):
        ph = k * 6.283 / count + rng.random() * .4
        tw = (rng.random() - .5) * 2.6
        far = r0 * .66
        near = r1 * .62

        def off(s, far=far, near=near):
            return far + (near - far) * s

        rk = (.4 + .22 * rng.random())
        flip = k % 2 == 0

        def lumps(s, flip=flip, rk=rk):
            q = s if flip else 1 - s
            # A forearm's swell toward the elbow, thin at the wrist.
            shape = .5 + .9 * math.sin(math.pi * min(1, q * 1.15)) ** 1.3
            return (r0 + (r1 - r0) * s) * rk * shape
        sp_, rad = strand(root, tip, tw, ph, off, lumps, n, seed + k * 3.1)
        tu(pc, sp_, 0, 'flesh-pale' if k % 4 == 0 else fin_a if k % 3 else fin_b, t.s(8), radii=rad)
    return d


def sleeve(pc, root, tip, r0, r1, fin, t, jag=.1, seed=1):
    """A torn coat sleeve round a limb from `root` to `tip`: a tube whose far end's rim is cut jagged, a button or two."""
    root, tip = Vector(root), Vector(tip)
    n = 6 if t.fine else 3
    segs = t.s(12)
    d = (tip - root).normalized()
    u = d.cross(Vector((.2, .3, 1))).normalized()
    v = d.cross(u)
    rng = random.Random(seed)
    rings = []
    for i in range(n + 1):
        s = i / n
        c = root + (tip - root) * s
        r = r0 + (r1 - r0) * s
        ring = []
        for k in range(segs):
            a = 6.283 * k / segs
            rr = r * (1 + .05 * math.sin(a * 3 + s * 9 + seed))
            drop = (rng.random() * jag * (1 if k % 2 else .35)) if i == n else 0
            ring.append(c + (u * math.cos(a) + v * math.sin(a)) * rr + d * drop)
        rings.append(ring)
    loft(pc, rings, lambda p: fin, True)
    # (Its inner side is the same surface seen through: gearkit's outline and the dark flesh behind it keep the read.)


def wristwatch(pc, centre, axis, radius, t, out):
    """A stopped wristwatch strapped round a limb: its strap, its case, the amber face toward `out`."""
    c, a, o = Vector(centre), Vector(axis).normalized(), Vector(out).normalized()
    rg(pc, c, a, radius, .018, 'leather', t.s(16), 4)
    f = c + o * (radius + .03)
    cy(pc, f - o * .025, f + o * .012, .05, 'steel', t.s(10))
    cy(pc, f + o * .012, f + o * .018, .04, 'amber', t.s(10))


def passenger_arm(d, base_r, y, a, length, size, t, fin='flesh', curl=.8):
    """A passenger's arm out of the bundle at height y and angle a round the limb: reaching and clawing, a sleeve at its root."""
    base = Vector((base_r * math.cos(a), y, base_r * math.sin(a)))
    out = Vector((math.cos(a), -.25, math.sin(a))).normalized()
    side = Vector((-math.sin(a), 0, math.cos(a)))
    wrist = base + out * length + side * .06 + Vector((0, -.05, 0))
    tu(d, [base - out * .05, base + out * length * .5 + Vector((0, .04, 0)), wrist], 0, fin, t.s(7, 4), radii=[size * .62, size * .52, size * .38])
    tu(d, [base - out * .03, base + out * length * .32 + Vector((0, .025, 0))], 0, 'cloth%d' % (int(a * 3) % 6), t.s(7, 4), radii=[size * .74, size * .68])
    hand(d, wrist, (out + Vector((0, -.2, 0))).normalized(), side, size, fin, t, curl=curl)


def passenger_face(d, base_r, y, a, size, t):
    """A face pushing out through the bundle, its eyes shut and its mouth open; a fold of flesh round it."""
    n = Vector((math.cos(a), 0, math.sin(a)))
    c = Vector((base_r * math.cos(a), y, base_r * math.sin(a)))
    side = Vector((-math.sin(a), 0, math.cos(a)))
    sp(d, c, (size, size * 1.22, size), 'flesh-pale', t.s(12), t.s(8))
    rg(d, c - n * size * .15, n, size * 1.0, size * .22, 'flesh-dark', t.s(14), t.s(6, 4))
    if t.fine:
        sp(d, c + n * size * .92 + Vector((0, -size * .42, 0)), (size * .34, size * .42, size * .14), 'black', 8, 5)
        for sg in (-1, 1):
            sp(d, c + n * size * .86 + side * sg * size * .38 + Vector((0, size * .24, 0)), (size * .17, size * .1, size * .08), 'black', 6, 4)
        sp(d, c + n * size * 1.0, (size * .1, size * .16, size * .1), 'flesh', 6, 4)


def leg_wheel(i, t):
    """The bogie wheel the leg grows from: a spoked iron wheel with a bright worn tread and flange, turned about x (the joint spins it)."""
    s = side_of(i)
    pc = Piece('wheel')
    pc.sharp = 35
    segs = t.s(28, 10)
    r_out, r_tread = .56, .52
    # The tyre and flange as a lathe about x.
    prof = ((-.07, .44), (-.07, r_tread), (.04, r_tread), (.04, r_out), (.075, r_out), (.075, .46))
    rings = []
    for x, r in prof:
        rings.append([(s * x, r * math.cos(6.283 * k / segs), r * math.sin(6.283 * k / segs)) for k in range(segs)])

    def fin(p):
        rr = math.hypot(p.y, p.z)
        return 'tread' if rr > .47 else 'iron'
    loft(pc, rings, fin, True)
    # The spokes, the hub, the axle's nut.
    spokes = 8 if t.fine else 6 if t.mid else 4
    for k in range(spokes):
        a = 6.283 * k / spokes
        d = Vector((0, math.cos(a), math.sin(a)))
        cy(pc, Vector((s * .0, 0, 0)) + d * .12, Vector((s * .0, 0, 0)) + d * .44, .03, 'iron-mid', t.s(6, 3), .022)
    cy(pc, (s * -.08, 0, 0), (s * .1, 0, 0), .15, 'iron', t.s(14))
    cy(pc, (s * .1, 0, 0), (s * .17, 0, 0), .08, 'steel', t.s(6))
    if t.mid:
        for k in range(6):
            a = 6.283 * k / 6
            cy(pc, (s * .1, .11 * math.cos(a), .11 * math.sin(a)), (s * .115, .11 * math.cos(a), .11 * math.sin(a)), .016, 'rivet', 6)
    return [pc.make(False)]


def leg_thigh(i, t):
    """A thigh: from the hub down -y, thick at the hub and tapering to the knee (+z: the side the knee bends toward)."""
    s, row = side_of(i), row_of(i)
    L = thigh(i)
    w = .44 if row == 0 else .4
    pc = Piece('thigh')
    pc.sharp = 70
    root, tip = (0, .04, 0), (0, -L + .1, 0)
    limb_bundle(pc, root, tip, w, w * .66, t, 11 + i, 7)
    # The socket: flesh gathered round the wheel's hub, an iron band bolted over it.
    sp(pc, (0, -.03, 0), (w * 1.02, w * 1.05, w * 1.02), 'flesh-dark', t.s(14), t.s(8))
    d = Piece('thigh-detail')
    d.sharp = 40
    rg(d, (0, -.38, 0), (0, 1, 0), w * .92, .035, 'iron', t.s(18), 5)
    if t.mid:
        for k in range(6):
            a = 6.283 * k / 6
            cy(d, (w * .94 * math.cos(a), -.38, w * .94 * math.sin(a)), (w * 1.0 * math.cos(a), -.38, w * 1.0 * math.sin(a)), .02, 'rivet', 6)
    # A coat sleeve, torn off at its elbow, and a second over the lower thigh.
    cl = 'cloth%d' % i
    cl2 = 'cloth%d' % ((i + 3) % 6)
    sleeve(d, (0, -.1, 0), (0, -L * .46, 0), w * 1.1, w * .92, cl, t, .22, i + 1)
    if t.mid:
        sleeve(d, (0, -L * .62, 0), (0, -L * .84, 0), w * .84, w * .76, cl2, t, .14, i + 7)
        # The brake rod strapped along its top, in two belts; veins running amber between the strands.
        rod = w * 1.12
        cy(d, (0, -L * .16, rod), (0, -L * .86, rod * .8), .03, 'steel', t.s(6))
        for y, r in ((-L * .3, w * 1.1), (-L * .72, w * .86)):
            rg(d, (0, y, 0), (0, 1, 0), r, .022, 'leather', t.s(16), 4)
            bx(d, (0, y, r), (.06, .05, .03), 'brass')
        for k in range(4 if t.fine else 2):
            a = k * 1.7 + i
            pts = cm([(w * .86 * math.cos(a), -L * .2, w * .86 * math.sin(a)), (w * .78 * math.cos(a + .4), -L * .45, w * .78 * math.sin(a + .4)), (w * .7 * math.cos(a + .1), -L * .78, w * .7 * math.sin(a + .1))], 3)
            tu(d, pts, .012, 'vein', 4)
        # Coat buttons on the sleeve; a passenger's own hand out of the bundle, reaching.
        for k in range(3):
            a = 2.2 + k * .22
            cy(d, (w * 1.0 * math.cos(a), -L * .32, w * 1.0 * math.sin(a)), (w * 1.02 * math.cos(a), -L * .32, w * 1.02 * math.sin(a)), .02, 'brass', 6)
    if t.mid:
        # What the limb is made of, coming apart: an arm reaching out of it, a face in it.
        a = 1.2 + i * 1.1
        passenger_arm(d, w * .62, -L * .56, a, .36, .13, t)
        passenger_face(d, w * .66, -L * .3, a + 2.4, .13, t)
    if t.fine:
        passenger_arm(d, w * .6, -L * .72, a + 3.6, .3, .11, t, 'flesh-pale')
        passenger_face(d, w * .58, -L * .8, a + 1.3, .1, t)
    return [pc.make(), d.make()]


def leg_shin(i, t):
    """The knee's knot and the shin below it, tapering to the wrist: an axle box on the knee's outer face (its brass cap), stopped
    wristwatches in a row toward the hand."""
    row = row_of(i)
    L = shin(i)
    w = .42 if row == 0 else .38
    pc = Piece('shin')
    pc.sharp = 70
    limb_bundle(pc, (0, .1, 0), (0, -L + .08, 0), w * .78, w * .46, t, 31 + i, 6)
    sp(pc, (0, 0, 0), (w * .82, w * .8, w * .82), 'flesh-dark', t.s(14), t.s(8))
    d = Piece('shin-detail')
    d.sharp = 40
    # The axle box: an iron casting on the knee, its bolts, its brass end cap, a grease nipple.
    bx(d, (0, .02, w * .36), (w * 1.1, .46, w * .7), 'iron')
    cy(d, (0, .02, w * .7), (0, .02, w * .78), .13, 'brass', t.s(12))
    if t.mid:
        for k in range(6):
            a = 6.283 * k / 6
            cy(d, (.1 * math.cos(a), .02 + .1 * math.sin(a), w * .78), (.1 * math.cos(a), .02 + .1 * math.sin(a), w * .8), .015, 'rivet', 6)
        for sx in (-1, 1):
            for sy in (-1, 1):
                cy(d, (sx * w * .46, .02 + sy * .18, w * .71), (sx * w * .46, .02 + sy * .18, w * .74), .02, 'iron-mid', 6)
    cl = 'cloth%d' % ((i + 3) % 6)
    sleeve(d, (0, -.14, 0), (0, -L * .34, 0), w * .86, w * .68, cl, t, .2, i + 13)
    if t.mid:
        # The cuff at the wrist, its buttons; the watches: every wrist in the bundle stopped at 6:47.
        for k in range(4 if t.fine else 2):
            y = -L * (.55 + k * .09)
            r = (w * .78 + (w * .46 - w * .78) * (-y / L)) * .9
            a = k * 2.1 + i
            wristwatch(d, (0, y, 0), (0, 1, 0), r, t, (math.cos(a), 0, math.sin(a)))
        for k in range(3):
            a = k * 2.2 + i * .7
            pts = cm([(w * .6 * math.cos(a), -L * .24, w * .6 * math.sin(a)), (w * .52 * math.cos(a + .5), -L * .55, w * .52 * math.sin(a + .5)), (w * .38 * math.cos(a), -L * .84, w * .38 * math.sin(a))], 3)
            tu(d, pts, .011, 'vein', 4)
        passenger_arm(d, w * .52, -L * .42, .7 + i * 1.9, .3, .1, t, 'flesh-pale')
    if t.fine:
        passenger_face(d, w * .46, -L * .5, 2.8 + i * 1.4, .085, t)
    return [pc.make(), d.make()]


def leg_hand(i, t):
    """A hand for a foot: palm down at the floor (the wrist 14 cm over it), fingers along +z, knuckles up, split nails; the coat cuff at
    the wrist. The front pair are larger: they are arms."""
    row = row_of(i)
    hs = 1.3 if row == 0 else 1.1
    pc = Piece('hand')
    pc.sharp = 70
    y_palm = -.05
    # The heel of the hand under the wrist, the palm pad, the knuckle ridge.
    sp(pc, (0, y_palm, .04 * hs), (.28 * hs, .13, .3 * hs), 'flesh', t.s(14), t.s(8))
    bx(pc, (0, y_palm, .28 * hs), (.5 * hs, .17, .42 * hs), 'flesh')
    if t.mid:
        sp(pc, (0, y_palm + .1, .46 * hs), (.26 * hs, .05, .08 * hs), 'flesh-dark', t.s(10), t.s(5))
    fingers = 4 if t.mid else 2
    for k in range(fingers):
        x = (k - (fingers - 1) * .5) * (.135 if t.mid else .27) * hs
        length = (.52 if k in (1, 2) else .44) * hs
        base = Vector((x, y_palm + .04, .46 * hs))
        ang = math.radians((k - (fingers - 1) * .5) * 8)
        d = Vector((math.sin(ang), 0, math.cos(ang)))
        p1 = base + d * length * .38 + Vector((0, .04, 0))
        p2 = p1 + d * length * .34 - Vector((0, .02, 0))
        p3 = p2 + d * length * .28 - Vector((0, .1, 0))
        r = (.058 if t.mid else .11) * hs
        tu(pc, [base, p1, p2, p3], 0, 'flesh', t.s(7, 4), radii=[r, r * .96, r * .84, r * .62])
        if t.fine:
            sp(pc, p1, r * 1.05, 'flesh-dark', 7, 4)
            sp(pc, p2, r * .92, 'flesh-dark', 7, 4)
            sp(pc, p3 + Vector((0, r * .25, 0)) + d * .01, (r * .75, r * .25, r * 1.1), 'nail', 6, 4)
    if t.mid:
        s = side_of(i)
        b = Vector((-s * .22 * hs, y_palm, .12 * hs))
        p1 = b + Vector((-s * .22, 0, .2)) * hs
        p2 = p1 + Vector((-s * .1, -.04, .22)) * hs
        tu(pc, [b, p1, p2], 0, 'flesh', t.s(7, 4), radii=[.09 * hs, .075 * hs, .055 * hs])
    d = Piece('hand-detail')
    d.sharp = 40
    sleeve(d, (0, .34, -.04 * hs), (0, .02, -.02 * hs), .24 * hs, .27 * hs, 'cloth%d' % i, t, .08, i + 21)
    if t.mid:
        rg(d, (0, .08, -.02 * hs), (0, 1, 0), .265 * hs, .02, 'leather', t.s(16), 4)
        cy(d, (.2 * hs, .08, .14 * hs), (.215 * hs, .08, .15 * hs), .02, 'brass', 6)
    return [pc.make(), d.make()]


# ===========================================================================================================================
# The doors and the arms behind them, the windows and the arms out of them
# ===========================================================================================================================
def door_leaf(bone, t):
    side = -1 if bone.startswith('door-l') else 1
    lead = bone.endswith('front')
    pc = Piece('door')
    pc.sharp = 35
    h, w = 1.74, .64
    bx(pc, (0, -.42, 0), (.05, .9, w), 'green')
    bx(pc, (0, .3, 0), (.046, .54, w), 'cream')
    bx(pc, (0, .77, 0), (.05, .22, w), 'green')
    for z in (-1, 1):
        bx(pc, (0, .3, z * (w / 2 - .05)), (.046, .6, .1), 'cream')
    # The window: frame, amber pane (a hand flat on it), a brass handle, the rubber nosing where the leaves meet.
    bx(pc, (side * .03, .3, 0), (.015, .58, .42), 'frame')
    bx(pc, (side * .036, .3, 0), (.012, .5, .34), 'amber-dim')
    if t.mid:
        bx(pc, (0, 0, -.31 if lead else .31), (.056, h, .03), 'rubber')
        bx(pc, (side * .036, .12, -.2 if lead else .2), (.03, .3, .03), 'brass')
        bx(pc, (side * .03, -.35, 0), (.008, .7, .5), 'green-dark')
        bx(pc, (side * .036, .02, 0), (.008, .025, w), 'brass')
    if t.fine:
        # A hand pressed on the glass from inside, the word on the kick plate.
        bx(pc, (side * .045, .22, .03), (.006, .15, .1), 'shadow')
        for k in range(4):
            bx(pc, (side * .045, .34, .03 - .036 + k * .024), (.006, .09, .018), 'shadow')
    return [pc.make()]


def grab_arms(bone, t):
    side = -1 if bone.startswith('grab-l') else 1
    dy = (-.3, -.1, .12, .32, 0)
    dz = (-.22, .18, -.05, .26, -.36)
    n = 5 if t.mid else 2
    pc = Piece('grab')
    pc.sharp = 70
    if bone.endswith('hands'):
        for k in range(n):
            y0, z0 = dy[k] * .9, dz[k] * .9
            elbow = (0, y0, z0)
            wrist = (side * .68, dy[k], dz[k])
            tu(pc, [elbow, (side * .34, y0 + .02, z0), wrist], 0, 'flesh', t.s(7), radii=[.07, .06, .045])
            hand(pc, wrist, Vector((side, .05 * (k - 2), .1 * (k - 2))), Vector((0, -1, .3)), .15, 'flesh' if k % 2 else 'flesh-pale', t, curl=.65)
    else:
        for k in range(n):
            tu(pc, [(0, dy[k] * 1.0, dz[k]), (side * .6, dy[k], dz[k]), (side * 1.15, dy[k] * .9, dz[k] * .9)], 0, 'flesh' if k % 2 == 0 else 'flesh-dark', t.s(7), radii=[.09, .08, .07])
            if k < 3:
                sleeve(pc, (side * .03, dy[k], dz[k]), (side * .62, dy[k], dz[k]), .1, .095, 'cloth%d' % (k + side + 1), t, .12, k + 31)
        if t.mid:
            tu(pc, [(side * .3, .02, .02), (side * .75, 0, .02), (side * 1.1, -.02, .05)], .012, 'vein', 4)
    return [pc.make()]


def window_part(side, k, t):
    s = -1 if side == 0 else 1
    broken = k == 1 if s < 0 else k == 2
    pc = Piece('window')
    pc.sharp = 30
    y, z = 1.55, WINDOW_Z[k]
    # In the joint's frame: x out of the side wall (s), y up, z along.
    hh, hw = .39, .49

    def box_at(c, size, fin, axes=None):
        bx(pc, c, size, fin, axes)
    # The rubber gasket and the steel frame round the opening.
    for sg in (-1, 1):
        box_at((s * .012, sg * (hh - .02), 0), (.03, .05, hw * 2), 'frame')
        box_at((s * .012, 0, sg * (hw - .02)), (.03, hh * 2, .05), 'frame')
        if t.mid:
            box_at((s * .022, sg * (hh - .05), 0), (.012, .015, hw * 2 - .08), 'rubber')
            box_at((s * .022, 0, sg * (hw - .05)), (.012, hh * 2 - .08, .015), 'rubber')
    if broken:
        box_at((s * -.04, 0, 0), (.02, hh * 2 - .1, hw * 2 - .1), 'black')
        if t.mid:
            # The teeth of glass left in the frame, a body's shoulder in the dark behind.
            rng = random.Random(side * 7 + k)
            for i in range(9 if t.fine else 4):
                a = rng.random() * 6.28
                py, pz = math.cos(a) * (hh - .12) * 1.0, math.sin(a) * (hw - .12)
                box_at((s * .006, py, pz), (.008, .1 + rng.random() * .12, .05 + rng.random() * .1), 'shard', tilted(rx=rng.random() * 90))
            sp(pc, (s * -.1, -.1, 0), (.1, .22, .26), 'flesh-dark', t.s(10), t.s(5))
        return [pc.make()]
    # The lit pane: the amber glass, the passengers against it.
    box_at((s * -.02, 0, 0), (.02, hh * 2 - .08, hw * 2 - .08), 'amber')
    o = s * -.002
    dz = -.18 if k % 2 == 0 else .2
    if t.mid:
        # The crush: heads, necks and shoulders at their own heights, backlit, a few faces turned to the glass.
        rng = random.Random(side * 13 + k * 7)
        n = 5 if t.fine else 3
        for i in range(n):
            hz = (-.33 + i * .66 / max(1, n - 1)) + (rng.random() - .5) * .05
            hy = -.1 + (rng.random() - .5) * .16 - (.05 if i % 2 else 0)
            hw_, hh_ = .075 + rng.random() * .02, .09 + rng.random() * .03
            sp(pc, (o, hy, hz), (.018, hh_, hw_), 'shadow', t.s(10), t.s(6))
            # The shoulders falling away from the neck, an arm up across another's back.
            tu(pc, [(o, hy - hh_ * .7, hz), (o, hy - hh_ * 1.5, hz - .1), (o, -.32, hz - .17)], 0, 'shadow', t.s(5, 3), radii=[.035, .05, .06])
            tu(pc, [(o, hy - hh_ * .7, hz), (o, hy - hh_ * 1.5, hz + .1), (o, -.32, hz + .17)], 0, 'shadow', t.s(5, 3), radii=[.035, .05, .06])
            if t.fine and i % 2 == 0:
                # The face lit from the glass: its dark mouth and eyes.
                sp(pc, (o + s * .012, hy, hz), (.008, hh_ * .82, hw_ * .78), 'flesh-dark', 8, 5)
                sp(pc, (o + s * .022, hy - hh_ * .35, hz), (.004, .022, .026), 'black', 6, 3)
                for e in (-1, 1):
                    sp(pc, (o + s * .022, hy + hh_ * .15, hz + e * hw_ * .36), (.004, .012, .014), 'black', 5, 3)
    if t.fine:
        # An arm across the glass, the hand pressed flat, the hopper's bar, cracks.
        hand_y, hand_z = .16, -dz * 1.3
        tu(pc, [(s * .002, hand_y - .08, hand_z), (s * .002, -.14, hand_z - dz * .3), (s * .002, -.34, hand_z - dz * .4)], 0, 'flesh-dark', 6, radii=[.03, .035, .04])
        box_at((s * .002, hand_y - .1, hand_z), (.006, .14, .11), 'flesh-dark')
        for f in range(4):
            box_at((s * .002, hand_y + .02, hand_z - .04 + f * .028), (.006, .1, .02), 'flesh-dark', tilted(rx=(f - 1.5) * 7))
        box_at((s * .008, hh * .42, 0), (.012, .02, hw * 2 - .08), 'frame')
        box_at((s * .008, 0, 0), (.01, hh * 2 - .08, .016), 'frame')
        rng = random.Random(side * 3 + k * 5)
        for i in range(3):
            a = rng.random() * 3.14
            box_at((s * .004, (rng.random() - .5) * .3, (rng.random() - .5) * .5), (.004, .28, .005), 'shadow', tilted(rx=math.degrees(a)))
    return [pc.make()]


def window_arm(a, t):
    pc = Piece('warm')
    pc.sharp = 70
    s = -1 if a < 2 else 1
    cl = 'cloth%d' % ((a * 2 + 1) % 6)
    tu(pc, [(0, .02, 0), (0, -.28, 0), (0, -.56, 0)], 0, 'flesh', t.s(8), radii=[.075, .07, .06])
    sleeve(pc, (0, .04, 0), (0, -.26, 0), .095, .085, cl, t, .1, a + 41)
    if t.mid:
        sp(pc, (0, -.56, 0), .068, 'flesh-dark', t.s(8), t.s(5))
    return [pc.make()]


def window_hand(a, t):
    pc = Piece('whand')
    pc.sharp = 70
    tu(pc, [(0, 0, 0), (0, -.2, 0), (0, -.4, .01)], 0, 'flesh', t.s(8), radii=[.062, .055, .042])
    hand(pc, (0, -.4, .01), Vector((0, -1, .08)), Vector((0, 0, 1)), .14, 'flesh', t, curl=.5)
    if t.mid:
        wristwatch(pc, (0, -.36, .01), (0, 1, 0), .05, t, (0, 0, -1)) if a % 2 == 0 else None
    return [pc.make()]


def cab_glow(t):
    """The cab's light: its side panes, the left half of the windscreen (a slumped figure at the controls against it), the blind's
    letters; in the joint's frame (0, 1.45, 4.0 on the body)."""
    pc = Piece('cabglow')
    pc.sharp = 40
    o = Vector((0, 1.45, 4.0))
    for s in (-1, 1):
        bx(pc, Vector((s * 1.464, 1.75, 3.55)) - o, (.012, .5, .52), 'amber')
    screen = Vector((0, 1.86, 4.82))
    up = Vector((0, .6, -.45)).normalized()
    out = Vector((0, .45, .6)).normalized()
    across = Vector((1, 0, 0))
    bx(pc, screen + across * -.62 + out * .02 - o, (1.0, .56, .01), 'amber', (across, up, out))
    if t.mid:
        word(pc, '6.47 EVAC', screen + up * .35 + out * .05 - o, (-1, 0, 0), up, out, .026, .006, 'amber')
    if t.fine:
        # The driver's outline, slumped over the desk, his hand on the glass.
        sp(pc, screen + across * -.62 + up * -.12 + out * .03 - o, (.14, .17, .012), 'shadow', 8, 5)
        bx(pc, screen + across * -.62 + up * -.3 + out * .03 - o, (.42, .16, .01), 'shadow', (across, up, out))
    return [pc.make()]


def cab_dead(t):
    """The other half of the windscreen: cracked and dark, part of the body."""
    pc = Piece('cabdead')
    pc.sharp = 40
    screen = Vector((0, 1.86, 4.82))
    up = Vector((0, .6, -.45)).normalized()
    out = Vector((0, .45, .6)).normalized()
    across = Vector((1, 0, 0))
    bx(pc, screen + across * .62 + out * .02, (1.0, .56, .012), 'black', (across, up, out))
    if t.mid:
        rng = random.Random(21)
        for i in range(8 if t.fine else 3):
            bx(pc, screen + across * (.62 + (rng.random() - .5) * .8) + up * ((rng.random() - .5) * .44) + out * .03, (.14 + rng.random() * .2, .08 + rng.random() * .1, .008), 'dead-glass', tilted(rz=rng.random() * 90, rx=-25))
    return [pc.make()]


def markers_part(t):
    pc = Piece('markers')
    pc.sharp = 40
    o = Vector((0, 1.3, 5.25))
    for s in (-1, 1):
        cy(pc, Vector((s * 1.12, 1.3, 5.17)) - o, Vector((s * 1.12, 1.3, 5.24)) - o, .09, 'iron', t.s(10))
        cy(pc, Vector((s * 1.12, 1.3, 5.24)) - o, Vector((s * 1.12, 1.3, 5.26)) - o, .065, 'marker', t.s(10))
    return [pc.make()]


def lamp_part(t):
    pc = Piece('lamp')
    pc.sharp = 35
    s = t.s(20)
    cy(pc, (0, 0, -.06), (0, 0, .16), .36, 'iron', s)
    cy(pc, (0, 0, .15), (0, 0, .195), .3, 'steel', s)
    cy(pc, (0, 0, .19), (0, 0, .215), .27, 'lamp', s)
    if t.mid:
        bx(pc, (0, .33, .08), (.66, .07, .34), 'iron', tilted(rx=-10))
        rg(pc, (0, 0, .17), (0, 0, 1), .33, .02, 'iron-mid', s, 4)
        sp(pc, (0, 0, .2), (.25, .25, .08), 'lamp', t.s(14), t.s(4))
    if t.fine:
        bx(pc, (0, 0, .218), (.56, .025, .008), 'iron')
        bx(pc, (0, 0, .218), (.025, .56, .008), 'iron')
    return [pc.make()]


def heart_part(t):
    """The Heart: the passengers' mass bulging out through the torn end, burning hot from the middle; faces and arms in it. The lit
    core is the second sub-mesh; the presentation pulses it."""
    pc = Piece('heart')
    pc.sharp = 80
    rng = random.Random(647)
    sp(pc, (0, 0, .15), (1.05, .85, .6), 'flesh', t.s(20), t.s(12))
    sp(pc, (-.55, -.35, -.05), (.5, .45, .4), 'flesh-dark', t.s(14), t.s(8))
    sp(pc, (.6, .3, -.02), (.45, .5, .4), 'flesh', t.s(14), t.s(8))
    sp(pc, (.05, .05, -.32), (.55, .45, .18), 'core', t.s(16), t.s(10))
    if t.mid:
        for k in range(9 if t.fine else 4):
            a = k * 2.4
            r = .45 + (k % 3) * .12
            tu(pc, cm([(r * math.cos(a) * .4, r * math.sin(a) * .35, -.4), (r * math.cos(a + .5) * .7, r * math.sin(a + .5) * .6, -.46), (r * math.cos(a + 1) * 1.0, r * math.sin(a + 1) * .82, -.38)], 3), .014, 'vein', 4)
    if t.fine:
        # The faces pressed out of it, their arms round the light.
        for k in range(7):
            a = k * .9 + .4
            c = Vector((.68 * math.cos(a), .52 * math.sin(a), -.36 - .04 * (k % 2)))
            sp(pc, c, (.12, .15, .1), 'flesh-pale' if k % 2 else 'flesh', 10, 7)
            for s in (-1, 1):
                sp(pc, c + Vector((s * .045, .03, -.09)), (.025, .025, .018), 'black', 5, 4)
            sp(pc, c + Vector((0, -.06, -.095)), (.03, .04, .014), 'black', 6, 4)
            d = Vector((math.cos(a + 1.3), math.sin(a + 1.3), -.1)).normalized()
            wrist = c + d * .3 + Vector((0, 0, -.05))
            tu(pc, [c + Vector((0, -.1, .02)), c + d * .15 + Vector((0, -.05, -.03)), wrist], 0, 'flesh', 6, radii=[.05, .045, .035])
            hand(pc, wrist, d, Vector((0, 0, -1)), .09, 'flesh', t, curl=.6)
    return [pc.make()]


def chain_link(k, t):
    pc = Piece('chain')
    pc.sharp = 40
    turn = k % 2 == 1
    ax = (1, 0, 0) if turn else (0, 0, 1)
    if t.mid:
        # A forged oval link in its plane (each a quarter turn from the last), the last one's shackle.
        n = 12 if t.fine else 8
        pts = []
        for i in range(n):
            a = 6.283 * i / n
            u = .07 * math.cos(a)
            v = -.17 + .15 * math.sin(a)
            pts.append((0, v, u) if turn else (u, v, 0))
        tu(pc, pts, .02, 'rust', t.s(6, 4), closed=True)
    else:
        bx(pc, (0, -.17, 0), (.06, .3, .06), 'rust')
    if k == CHAIN_LINKS - 1:
        bx(pc, (0, -.42, .05), (.12, .14, .2), 'iron')
    return [pc.make(False)]


def cable_link(first, k, t):
    pc = Piece('cable')
    pc.sharp = 60
    cy(pc, (0, 0, 0), (0, -.62, 0), .045, 'hose', t.s(8, 4))
    if t.fine:
        for y in (-.15, -.45):
            rg(pc, (0, y, 0), (0, 1, 0), .05, .008, 'rust', 8, 3)
    if k == CABLE_LINKS - 1 and t.mid:
        for f in range(3):
            bx(pc, ((f - 1) * .04, -.68, 0), (.015, .14, .015), 'copper', tilted(rz=(f - 1) * 25))
        sp(pc, (0, -.74, 0), .05, 'live', t.s(8), t.s(5))
    elif k == CABLE_LINKS - 1:
        bx(pc, (0, -.66, 0), (.1, .08, .1), 'copper')
    return [pc.make()]


def breach(side, t):
    """The torn flank: steel lips bent outward round a gash, the passengers' mass glowing through it, heads and reaching arms."""
    s = -1 if side == 0 else 1
    pc = Piece('breach')
    pc.sharp = 25
    bx(pc, (s * .02, .15, 0), (.03, 1.3, 2.6), 'black')
    rng = random.Random(side + 5)
    lips = 14 if t.fine else 6 if t.mid else 3
    for k in range(lips):
        z = -1.3 + k * 2.6 / max(1, lips - 1)
        y = .84 if k % 2 == 0 else -.54
        fin_ = 'green' if y < .4 else 'cream'
        bx(pc, (s * (.03 + .03 * (k % 3)), y + rng.random() * .1, z), (.03, .42 + rng.random() * .2, .3 + rng.random() * .14), fin_, tilted(rx=rng.random() * 40 - 20, ry=s * (15 + rng.random() * 25), rz=s * (10 + rng.random() * 30)))
    sp(pc, (0, .1, -.3), (.14, .55, .9), 'flesh-dark', t.s(12), t.s(7))
    sp(pc, (s * .04, .12, .25), (.1, .42, .6), 'amber', t.s(10), t.s(6))
    if t.mid:
        sp(pc, (s * .06, .3, -.5), (.03, .25, .5), 'core', t.s(8), t.s(4))
        for k in range(7 if t.fine else 3):
            z, y = -1.0 + k * .36, .35 + (k % 2) * .18
            sp(pc, (s * .06, y, z), (.11, .13, .11), 'flesh-pale' if k % 2 else 'flesh', t.s(9), t.s(6))
            if k % 2 == 1:
                wrist = Vector((s * .22, y + .36, z + .12))
                tu(pc, [(s * .06, y + .02, z), (s * .14, y + .2, z + .05), wrist], 0, 'flesh', t.s(6), radii=[.05, .045, .035])
                hand(pc, wrist, Vector((s * .4, 1, .2)), Vector((s, -.3, 0)), .1, 'flesh', t)
    return [pc.make()]


# ===========================================================================================================================
# The Stationmaster's own parts (his body is the player's model; these ride his joints): the top hat, the hatch he is grown into,
# the frock coat's tails, the watch's minute hand
# ===========================================================================================================================
def top_hat(t):
    """A top hat with a gold band and the railway's badge, a dent in its crown; the joint is its brim's underside at his crown."""
    pc = Piece('hat')
    pc.sharp = 45
    r = RIDER
    segs = t.s(24, 8)
    prof = ((.0, .0), (.25, .004), (.262, .02), (.25, .036), (.168, .04), (.162, .12), (.166, .28), (.18, .46), (.186, .49), (.17, .5), (0, .5))
    rings = []
    low = -.16
    for rr, y in prof:
        rings.append([(rr * r * math.cos(6.283 * k / segs), y * r + low, rr * r * math.sin(6.283 * k / segs)) for k in range(segs)])

    def push(q):
        # The dent in the crown's side, the brim's curl.
        d = math.hypot(q.x, q.z)
        if d > .2 * r:
            q = q + Vector((0, .05 * r * (q.x / d) ** 2 * ((d - .2 * r) / (.06 * r)), 0))
        a = (q - Vector((.12 * r, .36 * r + low, .08 * r))).length
        if a < .1 * r:
            q = q - Vector((.03 * r, 0, .02 * r)) * (1 - a / (.1 * r))
        return q
    loft(pc, rings, lambda p: 'hat', True, None, push)
    d = Piece('hat-band')
    d.sharp = 40
    cy(d, (0, .05 * r + low, 0), (0, .11 * r + low, 0), .17 * r, 'gold', segs)
    if t.mid:
        bx(d, (0, .08 * r + low, .175 * r), (.07 * r, .05 * r, .012), 'gold')
    return [pc.make(False), d.make()]


def hatch(t):
    """He is grown into the roof here: an iron hatch ring, its lid thrown back, torn roof round it, flesh rising round his waist in lumps
    with passengers' hands gripping him, the frock coat's skirt spread over it, cables down into the cab."""
    r = RIDER
    pc = Piece('hatch')
    pc.sharp = 35
    rg(pc, (0, 0, 0), (0, 1, 0), .56 * r, .07 * r, 'iron', t.s(24), t.s(8, 4))
    if t.mid:
        bx(pc, (0, .36 * r, -.62 * r), (.9 * r, .05 * r, .9 * r), 'roof', tilted(rx=-72))
        for k in range(8 if t.fine else 4):
            a = 6.283 * (k + .5) / (8 if t.fine else 4)
            cy(pc, (.56 * r * math.cos(a), .04, .56 * r * math.sin(a)), (.56 * r * math.cos(a), .1, .56 * r * math.sin(a)), .03, 'rivet', 6)
    fl = Piece('hatch-flesh')
    fl.sharp = 80
    sp(fl, (0, .06 * r, 0), (.42 * r, .22 * r, .36 * r), 'flesh-dark', t.s(16), t.s(9))
    if t.mid:
        rng = random.Random(47)
        for k in range(9 if t.fine else 4):
            a = rng.random() * 6.283
            d = .3 + rng.random() * .14
            sp(fl, (d * r * math.cos(a), .1 * r + rng.random() * .1, d * r * math.sin(a)), (.13 * r, .1 * r + rng.random() * .05, .13 * r), 'flesh' if k % 3 else 'flesh-pale', t.s(9), t.s(6))
        for a in (.8, 2.5, 4.0, 5.4):
            tu(fl, cm([(.24 * r * math.cos(a), .26 * r, .2 * r * math.sin(a)), (.42 * r * math.cos(a + .2), .1 * r, .34 * r * math.sin(a + .2)), (.6 * r * math.cos(a + .3), -.02, .48 * r * math.sin(a + .3))], 3), .013, 'vein', 4)
        tu(fl, cm([(-.2 * r, .24 * r, -.26 * r), (-.42 * r, .1 * r, -.5 * r), (-.56 * r, -.05, -.86 * r)], 4), .045, 'hose', t.s(6))
        tu(fl, cm([(.18 * r, .26 * r, -.26 * r), (.36 * r, .1 * r, -.56 * r), (.42 * r, -.05, -.96 * r)], 4), .04, 'hose', t.s(6))
    if t.fine:
        # Hands out of the mass, closing on his coat.
        for a in (.5, 2.1, 3.7, 5.1):
            c = Vector((.44 * r * math.cos(a), .12 * r, .38 * r * math.sin(a)))
            u = Vector((-math.cos(a), .45, -math.sin(a))).normalized()
            hand(fl, c, u, Vector((-math.cos(a), -.1, -math.sin(a))), .12, 'flesh-pale', t, curl=.9)
    # The coat's skirt laid out round the waist, a fold at a time.
    sk = Piece('hatch-coat')
    sk.sharp = 55
    n = 10 if t.fine else 6
    for k in range(n):
        a = 360.0 * k / n
        x, z = math.sin(math.radians(a)) * .34 * r, math.cos(math.radians(a)) * .3 * r
        bx(sk, (x, .2 * r, z), (.26 * r, .3 * r, .05 * r), 'coat' if k % 3 else 'coat-dark', tilted(ry=a, rx=22))
    return [pc.make(), fl.make(), sk.make()]


def uniform(t):
    """What the Stationmaster wears over his frock coat, on his chest joint (Runtime: at his own spine_05, his model 1.8 times a pilot):
    gold epaulettes with their fringes, a braid across the chest from the shoulder, brass buttons, the watch's chain, a lapel badge."""
    k = 1.8
    pc = Piece('uniform')
    pc.sharp = 40
    for s_ in (-1, 1):
        at = Vector((s_ * .165 * k, .135 * k, -.005 * k))
        bx(pc, at, (.115 * k, .016 * k, .085 * k), 'gold', tilted(rz=-s_ * 8))
        if t.mid:
            sp(pc, at + Vector((-s_ * .06 * k, .004, 0)), (.022 * k, .012 * k, .035 * k), 'brass', t.s(8), t.s(5))
        n = 9 if t.fine else 4 if t.mid else 0
        for f in range(n):
            u = (f + .5) / n - .5
            top = at + Vector((s_ * .056 * k, -.006, u * .08 * k))
            tu(pc, cm([top, top + Vector((s_ * .012 * k, -.022 * k, 0)), top + Vector((s_ * .015 * k, -.045 * k, u * .006))], 2), .0052 * k, 'gold', t.s(5, 3))
        for b in range(5 if t.mid else 0):
            cy(pc, (s_ * .052 * k, (.085 - b * .046) * k, .118 * k), (s_ * .052 * k, (.085 - b * .046) * k, .124 * k), .0085 * k, 'brass', t.s(8, 4))
    if t.mid:
        # The braid: looped from the right shoulder across the chest to the third button.
        rope = cm([(.14 * k, .118 * k, .07 * k), (.09 * k, .07 * k, .116 * k), (.0, .03 * k, .128 * k), (-.052 * k, -.008 * k, .124 * k)], 5)
        tu(pc, rope, .0062 * k, 'gold', t.s(5, 3))
        rope2 = cm([(.15 * k, .108 * k, .06 * k), (.1 * k, .04 * k, .112 * k), (.01 * k, -.012 * k, .126 * k), (-.052 * k, -.008 * k, .124 * k)], 5)
        tu(pc, rope2, .005 * k, 'gold', t.s(5, 3))
        bx(pc, (-.075 * k, .086 * k, .114 * k), (.032 * k, .022 * k, .006 * k), 'gold')
    return [pc.make()]


def tail_link(side, k, t):
    r = RIDER
    pc = Piece('tail')
    pc.sharp = 50
    w0, w1 = .3 - k * .05, .25 - k * .05
    L, th = TAIL_LENGTH + .02, .03
    n = 3 if t.mid else 1
    rings = []
    for i in range(n + 1):
        s = i / n
        wd = w0 + (w1 - w0) * s
        y = -L * s
        ring = [(-wd / 2, y, th / 2), (wd / 2, y, th / 2), (wd / 2 + .004, y, -th / 2), (-wd / 2 - .004, y, -th / 2)]
        rings.append(ring)
    loft(pc, rings, lambda p: 'coat', True)
    bx(pc, (0, -L, 0), (w1, .006, th), 'coat-dark')
    bx(pc, (0, 0, 0), (w0, .006, th), 'coat-dark')
    if t.fine:
        sx = -1 if side == 0 else 1
        bx(pc, (sx * (w0 + w1) / 4, -L / 2, th / 2 + .003), (.022, L * .98, .004), 'gold', tilted(rz=sx * math.degrees(math.atan2((w0 - w1) / 2, L))))
        if k == TAIL_LINKS - 1:
            bx(pc, (0, -L + .02, th / 2 + .003), (w1 * .9, .02, .004), 'gold')
    return [pc.make(False)]


def watch_hand(which, t):
    """The pocket watch's minute hand, at 6:47 or a minute on, round the dial's centre (right_hand), in the joint's frame: Core
    NativeStationBossLook.WatchPivot's place on the right hand, both frames the same place. The rig shows one or the other."""
    if not t.fine:
        return []
    pc = Piece('watch')
    r = RIDER
    centre = Vector((0, -.2 * r, .09 * r + .03 * r)) - Vector(WATCH_PIVOT)
    a = math.radians(47 * 6 if which == 47 else 48 * 6)
    # The dial is read from +z: clockwise from 12 runs toward -x (x is the boss's right, seen from its front).
    d = Vector((-math.sin(a), math.cos(a), 0))
    L = .088 * r
    u = Vector((d.y, -d.x, 0))
    bx(pc, centre + d * L * .5, (.009, L, .005), 'black', (u, d, (0, 0, 1)))
    return [pc.make(False)]


def right_hand(t):
    """The pocket watch held in the right hand, stopped: its case on a chain, the amber face, the hour hand fixed at 6 (the minute hand
    is its own joint)."""
    r = RIDER
    pc = Piece('rh')
    pc.sharp = 35
    c = Vector((0, -.2 * r, .09 * r))
    s = t.s(20)
    cy(pc, c + Vector((0, 0, -.02 * r)), c + Vector((0, 0, .02 * r)), .12 * r, 'gold', s)
    cy(pc, c + Vector((0, 0, .02 * r)), c + Vector((0, 0, .028 * r)), .1 * r, 'amber', s)
    if t.mid:
        rg(pc, c + Vector((0, 0, .02 * r)), (0, 0, 1), .105 * r, .006 * r, 'gold', s, 4)
        cy(pc, c + Vector((0, .12 * r, 0)), c + Vector((0, .16 * r, 0)), .014 * r, 'gold', 6)
        tu(pc, cm([c + Vector((0, .16 * r, 0)), c + Vector((.02, .3 * r, -.01)), (0, -.02, .05)], 4), .006 * r, 'gold', 4)
    if t.fine:
        # The hour hand, just short of seven.
        ah = math.radians(6.78 * 30)
        dh = Vector((-math.sin(ah), math.cos(ah), 0))
        bx(pc, c + dh * .03 * r + Vector((0, 0, .031 * r)), (.012, .06 * r, .004), 'black', (Vector((dh.y, -dh.x, 0)), dh, (0, 0, 1)))
        for k in range(12):
            a = k * .5236
            d_ = Vector((math.sin(a), math.cos(a), 0))
            bx(pc, c + d_ * .086 * r + Vector((0, 0, .031 * r)), (.006, .018, .003), 'black', (Vector((d_.y, -d_.x, 0)), d_, (0, 0, 1)))
    return [pc.make()]


def left_hand(t):
    """The last-call bell in the left hand: a turned brass bell with its wooden handle."""
    r = RIDER
    pc = Piece('lh')
    pc.sharp = 35
    s = t.s(18)
    prof = ((.03, 0.0), (.06, .08), (.14, .2), (.17, .26), (.165, .27), (0.0, .27))
    rings = []
    for rr, y in prof:
        rings.append([(rr * r * math.cos(6.283 * k / s), -.15 * r - y * r, rr * r * math.sin(6.283 * k / s)) for k in range(s)])
    loft(pc, rings, lambda p: 'brass', True)
    cy(pc, (0, -.02 * r, 0), (0, -.15 * r, 0), .03 * r, 'leather-tan', t.s(8))
    if t.mid:
        sp(pc, (0, -.42 * r, 0), .03 * r, 'iron', t.s(8), t.s(5))
    return [pc.make(False)]


# ===========================================================================================================================
# The build
# ===========================================================================================================================
def parts(t):
    """Every drawn joint and what makes it. Joints without one are only frames (the root, the Stationmaster's own body, which is the
    player's model)."""
    table = {'body': body_parts, 'lamp': lamp_part, 'heart': heart_part, 'roof': roof_panel, 'markers': markers_part, 'cab-glow': cab_glow, 'sm-hips': hatch, 'sm-hat': top_hat, 'sm-r-hand': right_hand, 'sm-l-hand': left_hand, 'sm-chest': uniform,
             'watch-647': (lambda t: watch_hand(47, t)), 'watch-648': (lambda t: watch_hand(48, t))}
    for name in ('panto-low', 'panto-high', 'panto-head'):
        table[name] = (lambda t, n=name: pantograph(n, t))
    for side, name in ((0, 'horn-l'), (1, 'horn-r')):
        table[name] = (lambda t, s=side: horn(s, t))
    for i in range(LEGS):
        n = leg_name(i)
        table['wheel-' + n] = (lambda t, i=i: leg_wheel(i, t))
        table['hip-' + n] = (lambda t, i=i: leg_thigh(i, t))
        table['knee-' + n] = (lambda t, i=i: leg_shin(i, t))
        table['foot-' + n] = (lambda t, i=i: leg_hand(i, t))
    for name in ('door-l-front', 'door-l-back', 'door-r-front', 'door-r-back'):
        table[name] = (lambda t, n=name: door_leaf(n, t))
    for name in ('grab-l', 'grab-l-hands', 'grab-r', 'grab-r-hands'):
        table[name] = (lambda t, n=name: grab_arms(n, t))
    for k in range(CHAIN_LINKS):
        table['chain-%d' % k] = (lambda t, k=k: chain_link(k, t))
    for k in range(CABLE_LINKS):
        table['cable-a-%d' % k] = (lambda t, k=k: cable_link('a', k, t))
        table['cable-b-%d' % k] = (lambda t, k=k: cable_link('b', k, t))
    for a in range(WINDOW_ARMS):
        table['window-arm-%d' % a] = (lambda t, a=a: window_arm(a, t))
        table['window-hand-%d' % a] = (lambda t, a=a: window_hand(a, t))
    for side in range(2):
        table['breach-' + 'lr'[side]] = (lambda t, s=side: breach(s, t))
        table['tail-%s-0' % 'lr'[side]] = (lambda t, s=side: tail_link(s, 0, t))
        table['tail-%s-1' % 'lr'[side]] = (lambda t, s=side: tail_link(s, 1, t))
        table['tail-%s-2' % 'lr'[side]] = (lambda t, s=side: tail_link(s, 2, t))
        for k in range(4):
            table['window-%s-%d' % ('lr'[side], k)] = (lambda t, s=side, k=k: window_part(s, k, t))
    return table


# The body's parts are authored in the body's frame, as every joint's in its own.
def bake_joint(name, fn, tier, seed):
    global LIMB
    gk.clear_scene()
    obs = fn(tier)
    LIMB = None
    for prefix, r0, r1 in (('hip-', .44, .29), ('knee-', .33, .19)):
        if name.startswith(prefix):
            n = name[len(prefix):]
            leg = next(i for i in range(LEGS) if leg_name(i) == n)
            k = 1.0 if row_of(leg) == 0 else .91
            LIMB = (r0 * k, r1 * k, thigh(leg) if prefix == 'hip-' else shin(leg))
    if name == 'body':
        obs = obs + cab_dead(tier)
    obs = [o for o in obs if o is not None]
    if not obs:
        return None
    return gk.bake('647-%s-%d' % (name, tier.level), obs, TO_JOINT, seed)


def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument('--out', required=False, help='Assets/LocalAssets to write the bank into')
    ap.add_argument('--preview', help='render each joint with its vertex colours')
    ap.add_argument('--sheet', help='render the assembled boss standing')
    ap.add_argument('--sheet-tier', type=int, default=0, help='the tier the sheet shows')
    ap.add_argument('--only', help='joints, comma separated')
    ap.add_argument('--tiers', default='0,1,2')
    args = ap.parse_args(argv)
    finishes()
    only = set(args.only.split(',')) if args.only else None
    bank = gk.GearBank()
    built = {}
    totals = {}
    for level in [int(x) for x in args.tiers.split(',')]:
        t = Tier(level)
        table = parts(t)
        for idx, (name, fn) in enumerate(table.items()):
            if only and name not in only:
                continue
            gm = bake_joint(name, fn, t, idx * .37)
            if gm is None:
                continue
            prop = '647-%s-%d' % (name, level)
            bank.add(prop, name, gm)
            built[prop] = gm
            totals[level] = totals.get(level, 0) + sum(len(s) for s in gm.submeshes) // 3
            if args.preview:
                gk.clear_scene()
                gk.preview(gm, TO_JOINT, args.preview, prop)
    print('triangles per tier', totals)
    if args.sheet:
        import station_boss_sheet as sheet
        sheet.render(built, TO_JOINT, args.sheet, rest, JOINTS, args.sheet_tier)
    if args.out:
        bank.write(args.out, BANK)


if __name__ == '__main__':
    main()
