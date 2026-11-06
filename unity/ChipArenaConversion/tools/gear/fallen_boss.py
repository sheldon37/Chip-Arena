"""NOCTIEL, the final boss (docs/EXPEDITION-BOSS-FINAL.md): the plate, the robe, the six wings, the halo and the claymore, modelled in Blender and
written as the local bank Resources/ChipArenaPackBossFallen (gearkit.py: the bake, the bone frames, the bank).

usage: blender --background --factory-startup --python tools/gear/fallen_boss.py -- --out ASSETS/LocalAssets [--sheet DIR] [--tiers 0,1]

Everything is modelled once in the player's skeleton's rest pose (the fits are the bone rest frames below, measured from the local tool's bind dump; no mesh of
the bake is read), about 1.76 m tall: the game draws it scaled to about 3.5 m. A piece rides one bone as a plain mesh under it; the project skins on the CPU.
Wings, robe strips and the halo are chains the game turns itself, each segment baked in its own pivot's frame. Black, white and grey only: the one light is white.
Props are named fb-<part>-<tier>, and Core NativeFallenBossLook.Parts says which phases show each.
"""
import argparse
import math
import os
import random
import sys

import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gearkit as gk  # noqa: E402
from gearkit import Piece, cyl, finish, sphere, torus, tube  # noqa: E402

BANK = 'ChipArenaPackBossFallen'
V = Vector
# The design frame (Blender: X the pilot's right, Y forward, Z up) to the mesh space the bind poses are in (x right, y up, z forward).
M = Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
# Each bone's rest in mesh space: origin, then its x, y, z axes (the local tool's bind dump).
REST = {
    'pelvis': ((0, .987, .024), (0, -1, .06), (0, -.06, -1), (1, 0, 0)),
    'spine_02': ((0, 1.075, .036), (0, -.99, -.12), (0, .12, -.99), (1, 0, 0)),
    'spine_03': ((0, 1.136, .043), (0, -1, .06), (0, -.06, -1), (1, 0, 0)),
    'spine_05': ((0, 1.395, .001), (0, -.98, .21), (0, -.21, -.98), (1, 0, 0)),
    'neck_01': ((0, 1.491, -.015), (0, -1, -.09), (0, .09, -1), (1, 0, 0)),
    'head': ((0, 1.573, -.005), (1, 0, 0), (0, 1, 0), (0, 0, 1)),
    'upperarm_l': ((-.133, 1.425, -.028), (.6, .8, .01), (-.01, .02, -1), (-.8, .6, .02)),
    'lowerarm_l': ((-.291, 1.215, -.03), (.61, .69, -.39), (-.29, -.26, -.92), (-.74, .67, .04)),
    'hand_l': ((-.427, 1.06, .058), (.48, .78, -.4), (.72, -.61, -.33), (-.5, -.13, -.86)),
    'upperarm_r': ((.133, 1.425, -.028), (.6, -.8, -.01), (-.01, -.02, 1), (-.8, -.6, -.02)),
    'lowerarm_r': ((.291, 1.215, -.03), (.61, -.69, .39), (-.29, .26, .92), (-.74, -.67, -.04)),
    'hand_r': ((.427, 1.06, .058), (.48, -.78, .4), (.72, .61, .33), (-.5, .13, .86)),
    'thigh_l': ((-.093, .961, .038), (-.02, -1, -.02), (0, .02, -1), (1, -.02, 0)),
    'calf_l': ((-.103, .536, .03), (-.03, -1, -.08), (.01, .08, -1), (1, -.03, .01)),
    'foot_l': ((-.117, .11, -.005), (.01, -1, -.05), (0, .05, -1), (1, .01, 0)),
    'ball_l': ((-.116, .013, .116), (0, .05, -1), (-.01, 1, .05), (1, .01, 0)),
    'thigh_r': ((.093, .961, .038), (-.02, 1, .02), (0, -.02, 1), (1, .02, 0)),
    'calf_r': ((.103, .536, .03), (-.03, 1, .08), (.01, -.08, 1), (1, .03, -.01)),
    'foot_r': ((.117, .11, -.005), (.01, 1, .05), (0, -.05, 1), (1, -.01, 0)),
    'ball_r': ((.116, .013, .116), (0, -.05, 1), (-.01, -1, -.05), (1, -.01, 0)),
}


def rest_matrix(b):
    o, x, y, z = REST[b]
    m = Matrix(((x[0], y[0], z[0], o[0]), (x[1], y[1], z[1], o[1]), (x[2], y[2], z[2], o[2]), (0, 0, 0, 1)))
    # The dump's axes are rounded here: orthonormalize so a piece doesn't shear.
    r = m.to_3x3()
    r.orthogonalize() if hasattr(r, 'orthogonalize') else None
    q = r.to_quaternion()
    return Matrix.Translation(o) @ q.to_matrix().to_4x4()


def to_bone(b):
    return rest_matrix(b).inverted() @ M


def J(b):
    """A bone's rest origin in the design frame."""
    o = REST[b][0]
    return V((o[0], o[2], o[1]))


def to_pivot(p):
    """Design to a chain segment's own frame: the mesh space's axes, its origin on the pivot `p` (design)."""
    return Matrix.Translation(-(M @ V(p))) @ M


# ---------------------------------------------------------------------------------------------------------------------------
# The sword's frame in hand_r: the game's greatsword (its grip, the blade's way, and the flat's up).
# ---------------------------------------------------------------------------------------------------------------------------
GRIP = V((.096, -.018, -.015))
BLADE = V((.144, .468, .872)).normalized()
FLAT = V((.349, -.848, .398))
FLAT = (FLAT - BLADE * FLAT.dot(BLADE)).normalized()
ACROSS = BLADE.cross(FLAT).normalized()


def to_sword():
    """Sword design (X across the edges, Y through the flats, Z up the blade, the fist's centre at the origin) to hand_r's frame."""
    m = Matrix(((ACROSS[0], FLAT[0], BLADE[0], GRIP[0]), (ACROSS[1], FLAT[1], BLADE[1], GRIP[1]), (ACROSS[2], FLAT[2], BLADE[2], GRIP[2]), (0, 0, 0, 1)))
    return m


def sword_in_design():
    """The sword's frame in the rest design frame (for the sheet)."""
    return M.inverted() @ rest_matrix('hand_r') @ to_sword()


# ---------------------------------------------------------------------------------------------------------------------------
# Finishes: black, white and grey. The toon's emission is the one light.
# ---------------------------------------------------------------------------------------------------------------------------
def finishes():
    finish('plate', 0xECE9E2, 'paint', under=0x8E8D8A, dirt=.35)
    finish('plate-trim', 0xB9BABC, 'metal', dirt=.3)
    finish('plate-dark', 0x4A4A4E, 'metal', dirt=.5)
    finish('cloth', 0x17171B, 'cloth', dirt=.2)
    finish('cloth-edge', 0x8E8E92, 'cloth', dirt=.2)
    finish('steel', 0xC8CCD0, 'metal', dirt=.15)
    finish('steel-dark', 0x34363A, 'metal', dirt=.3)
    finish('chain', 0x55585C, 'metal', outline=False, dirt=.3)
    finish('black', 0x050506, 'plastic', dirt=0)
    finish('vein', 0x030304, 'sticker', outline=False, dirt=0)
    finish('feather', 0xE8E5DE, 'cloth', outline=False, dirt=.25)
    finish('feather-dark', 0x9C9A96, 'cloth', outline=False, dirt=.3)
    finish('bone', 0xCFCBC0, 'plastic', dirt=.6)
    finish('light', 0xFFFFFF, 'glow')


# ---------------------------------------------------------------------------------------------------------------------------
# Lofting: smooth plates from rings
# ---------------------------------------------------------------------------------------------------------------------------
def ring(c, u, v, rx, ry, n, power=2.0, keel=0.0, spine=0.0):
    """A superellipse round `c` in the plane of u and v. `keel` pushes the +v side to a ridge, `spine` the -v side."""
    pts = []
    for i in range(n):
        a = 2 * math.pi * i / n
        ca, sa = math.cos(a), math.sin(a)
        x = math.copysign(abs(ca) ** (2 / power), ca) * rx
        y = math.copysign(abs(sa) ** (2 / power), sa) * ry
        front = max(0.0, sa)
        back = max(0.0, -sa)
        y += keel * front ** 6 - spine * back ** 6
        pts.append(c + u * x + v * y)
    return pts


def loft(pc, rings, fin, caps=True):
    bm = pc.bm
    n = len(rings[0])
    idx = pc.slot(fin)
    made = [[bm.verts.new(tuple(p)) for p in r] for r in rings]
    faces = []
    for a, b in zip(made, made[1:]):
        for i in range(n):
            j = (i + 1) % n
            faces.append(bm.faces.new((a[i], a[j], b[j], b[i])))
    if caps:
        faces.append(bm.faces.new(made[0][::-1]))
        faces.append(bm.faces.new(made[-1]))
    for f in faces:
        f.material_index = idx
    return faces


def limb(pc, a, b, stations, fin, n=16, side=None, power=2.0, keel=0.0, caps=True):
    """A loft along a to b: stations (t, half width, half depth, offset along the limb's front). `side` the limb's across way."""
    d = (b - a).normalized()
    u = (side if side is not None else V((1, 0, 0)))
    u = (u - d * u.dot(d)).normalized()
    v = d.cross(u).normalized()
    if v.y < 0:
        v = -v
    rings = []
    for st in stations:
        t, rx, ry = st[:3]
        off = st[3] if len(st) > 3 else 0.0
        rings.append(ring(a + (b - a) * t + v * off, u, v, rx, ry, n, power, keel))
    return loft(pc, rings, fin, caps)


def blade_shape(pc, base, along, across, thick, length, widths, fin, tip=True):
    """A flat, leaf or blade solid: `widths` half widths along it, diamond in section, closing to a point."""
    n = len(widths)
    bm = pc.bm
    idx = pc.slot(fin)
    rows = []
    for i, w in enumerate(widths):
        c = base + along * (length * i / (n - 1))
        rows.append([bm.verts.new(tuple(c + across * w)), bm.verts.new(tuple(c + thick * (w * .22 + .0005))), bm.verts.new(tuple(c - across * w)), bm.verts.new(tuple(c - thick * (w * .22 + .0005)))])
    for a, b in zip(rows, rows[1:]):
        for i in range(4):
            j = (i + 1) % 4
            bm.faces.new((a[i], a[j], b[j], b[i])).material_index = idx
    bm.faces.new(rows[0][::-1]).material_index = idx
    bm.faces.new(rows[-1]).material_index = idx


def rnd(seed):
    return random.Random(seed)


# ---------------------------------------------------------------------------------------------------------------------------
# The body. S is the ring count: tier 0 full, tier 1 reduced.
# ---------------------------------------------------------------------------------------------------------------------------
def undersuit(tier):
    """The black under everything: joints never show a gap. One piece per bone."""
    n = 10 if tier == 0 else 7
    parts = {}
    for bone, a, b, r0, r1 in (('upperarm_r', 'upperarm_r', 'lowerarm_r', .042, .034), ('lowerarm_r', 'lowerarm_r', 'hand_r', .034, .026), ('upperarm_l', 'upperarm_l', 'lowerarm_l', .042, .034), ('lowerarm_l', 'lowerarm_l', 'hand_l', .034, .026),
                               ('thigh_r', 'thigh_r', 'calf_r', .07, .05), ('calf_r', 'calf_r', 'foot_r', .05, .034), ('thigh_l', 'thigh_l', 'calf_l', .07, .05), ('calf_l', 'calf_l', 'foot_l', .05, .034)):
        pc = Piece('under-' + bone)
        cyl(pc, J(a), J(b), r0, 'cloth', segs=n, r1=r1)
        sphere(pc, J(a), (r0, r0, r0), 'cloth', u=n, v=6)
        sphere(pc, J(b), (r1, r1, r1), 'cloth', u=n, v=6)
        parts[bone] = pc.make()
    pc = Piece('under-spine')
    cyl(pc, J('pelvis') + V((0, 0, .05)), J('spine_03'), .09, 'cloth', segs=n, r1=.085)
    parts['spine_02'] = pc.make()
    pc = Piece('under-chest')
    cyl(pc, J('spine_03'), J('neck_01'), .09, 'cloth', segs=n, r1=.06)
    parts['spine_05'] = pc.make()
    pc = Piece('under-neck')
    cyl(pc, J('neck_01'), J('head') + V((0, 0, .02)), .04, 'black', segs=n, r1=.035)
    parts['neck_01'] = pc.make()
    return parts


def core_body(tier):
    """Phase 4: the figure of light inside the shell, one glowing piece per bone where the black was."""
    n = 8 if tier == 0 else 6
    parts = {}
    for bone, a, b, r0, r1 in (('upperarm_r', 'upperarm_r', 'lowerarm_r', .042, .034), ('lowerarm_r', 'lowerarm_r', 'hand_r', .034, .024), ('upperarm_l', 'upperarm_l', 'lowerarm_l', .042, .034), ('lowerarm_l', 'lowerarm_l', 'hand_l', .034, .024),
                               ('thigh_r', 'thigh_r', 'calf_r', .07, .05), ('calf_r', 'calf_r', 'foot_r', .05, .03), ('thigh_l', 'thigh_l', 'calf_l', .07, .05), ('calf_l', 'calf_l', 'foot_l', .05, .03)):
        pc = Piece('core-' + bone)
        cyl(pc, J(a), J(b), r0, 'light', segs=n, r1=r1)
        sphere(pc, J(a), (r0, r0, r0), 'light', u=n, v=5)
        parts[bone] = pc.make()
    pc = Piece('core-spine')
    cyl(pc, J('pelvis') + V((0, 0, .05)), J('spine_03'), .08, 'light', segs=n, r1=.075)
    parts['spine_02'] = pc.make()
    pc = Piece('core-chest')
    cyl(pc, J('spine_03'), J('neck_01'), .08, 'light', segs=n, r1=.05)
    parts['spine_05'] = pc.make()
    pc = Piece('core-neck')
    cyl(pc, J('neck_01'), J('head') + V((0, 0, .02)), .034, 'light', segs=n, r1=.03)
    parts['neck_01'] = pc.make()
    return parts


def head_light(tier):
    pc = Piece('head-light')
    sphere(pc, V((0, .012, 1.665)), (.07, .082, .098), 'light', u=14 if tier == 0 else 8, v=10 if tier == 0 else 6)
    return pc.make()


def torso_rings(n, zs, keel=.0):
    rings = []
    for z, rx, ry, oy, power in zs:
        rings.append(ring(V((0, oy, z)), V((1, 0, 0)), V((0, 1, 0)), rx, ry, n, power, keel))
    return rings


CHEST_RINGS = [(1.205, .112, .092, .036, 2.3), (1.25, .128, .102, .036, 2.3), (1.31, .150, .118, .03, 2.4), (1.37, .165, .124, .02, 2.5), (1.42, .158, .11, .005, 2.4), (1.465, .118, .085, -.012, 2.2), (1.495, .062, .062, -.018, 2.0)]


def cuirass(tier, broken=False):
    """The chest. `broken`: the left half and the sternum gone, the black glass and the light behind them (phase 3)."""
    n = 28 if tier == 0 else 16
    pc = Piece('cuirass')
    rings = torso_rings(n, CHEST_RINGS, .016)
    if broken:
        # The plate's right half only: pull the removed side's ring in to the glass so its edge reads as a tear.
        cut = rnd(3)
        torn = []
        for ri, r in enumerate(rings):
            out = []
            for p in r:
                edge = -.015 + .02 * math.sin(p.z * 60) + cut.uniform(-.008, .008)
                if p.x < edge and p.y > -.01:
                    q = V((p.x * .86, p.y * .8, p.z))
                else:
                    q = p
                out.append(q)
            torn.append(out)
        rings = torn
    loft(pc, rings, 'plate')
    # The breast's lames and a centre ridge in trim.
    for z, w in ((1.235, .118), (1.275, .132)):
        if not broken:
            r = ring(V((0, .036, z)), V((1, 0, 0)), V((0, 1, 0)), w + .004, w * .83 + .004, n, 2.3, .016)
            loft(pc, [r, [p + V((0, 0, .006)) for p in r]], 'plate-trim')
    # Engraving: the ridge down the sternum, feathers sweeping up and out from it, a border under the arms.
    if not broken:
        def surf(x, z):
            for (z0, rx0, ry0, oy0, p0), (z1, rx1, ry1, oy1, p1) in zip(CHEST_RINGS, CHEST_RINGS[1:]):
                if z0 <= z <= z1:
                    u = (z - z0) / (z1 - z0)
                    rx, ry, oy, pw = rx0 + (rx1 - rx0) * u, ry0 + (ry1 - ry0) * u, oy0 + (oy1 - oy0) * u, p0 + (p1 - p0) * u
                    q = min(.98, abs(x) / rx)
                    return V((x, oy + ry * (1 - q ** pw) ** (1 / pw) + .016 * max(0, 1 - abs(x) / .03) ** 3 + .0012, z))
            return V((x, .1, z))
        tube(pc, [surf(0, z) for z in (1.215, 1.26, 1.31, 1.36, 1.41, 1.45)], .0038, 'plate-trim', segs=5)
        if tier == 0:
            for side in (1, -1):
                for k in range(6):
                    z0 = 1.225 + k * .034
                    pts = [surf(side * (.006 + q * .112), z0 + q * .05 + q * q * .035) for q in (0, .17, .34, .5, .67, .84, 1)]
                    tube(pc, pts, .0016, 'plate-dark', segs=4, caps=False)
                pts = [surf(side * x, z) for x, z in ((.098, 1.215), (.126, 1.26), (.144, 1.31), (.15, 1.36), (.138, 1.42))]
                tube(pc, pts, .0026, 'plate-trim', segs=4, caps=False)
    # The collar: raised, open at the front.
    collar = []
    for t, h in ((0, .0), (.5, .03), (1, .055)):
        c = []
        for i in range(n):
            a = 2 * math.pi * i / n
            fr = max(0, math.sin(a))
            c.append(V((math.cos(a) * (.074 + .012 * t), -.018 + math.sin(a) * (.07 + .012 * t), 1.482 + h - fr * h * 1.2)))
        collar.append(c)
    loft(pc, collar, 'plate-trim')
    return pc.make()


def chest_glass(tier):
    """Phase 3 and 4: black glass where the plate was, white light moving inside (the light is its own part)."""
    n = 20 if tier == 0 else 12
    pc = Piece('glass')
    rs = [(1.215, .098, .082, .036, 2.2), (1.3, .132, .104, .03, 2.3), (1.37, .146, .108, .02, 2.3), (1.44, .11, .08, .0, 2.2)]
    loft(pc, torso_rings(n, rs), 'black')
    return pc.make()


def chest_light(tier):
    """The light inside the chest: a heart and rivers from it. Its own glowing part."""
    pc = Piece('heart')
    sphere(pc, V((-.03, .13, 1.35)), (.036, .02, .05), 'light', u=12, v=8)
    r = rnd(11)
    for i in range(9 if tier == 0 else 5):
        a = i * 2 * math.pi / 9 + r.uniform(-.2, .2)
        pts = [V((-.03, .13, 1.35))]
        p = V((-.03, .13, 1.35))
        for k in range(4):
            p = p + V((math.cos(a) * .035, 0, math.sin(a) * .04)) + V((r.uniform(-.012, .012), 0, r.uniform(-.012, .012)))
            pts.append(V((p.x, .118 - .012 * k, p.z)))
        tube(pc, gk.catmull(pts, per=3), .0035, 'light', segs=4, radii=[.0045 - .0035 * j / 12 for j in range(13)] if False else None)
    return pc.make()


def cracks(tier, seed, bone_pts):
    """Hairline cracks with light behind them (phase 2): thin glowing strips along a list of (start, way, length)."""
    pc = Piece('cracks-%d' % seed)
    r = rnd(seed)
    for start, way, length in bone_pts:
        p = V(start)
        pts = [p.copy()]
        steps = 6
        for k in range(steps):
            p = p + V(way) * (length / steps) + V((r.uniform(-.012, .012), r.uniform(-.004, .004), r.uniform(-.012, .012)))
            pts.append(p.copy())
        tube(pc, pts, .0028, 'light', segs=4)
    return pc.make()


def abdomen(tier):
    n = 24 if tier == 0 else 14
    pc = Piece('abdomen')
    rs = [(1.05, .118, .094, .036, 2.3), (1.1, .1, .082, .04, 2.2), (1.15, .098, .08, .042, 2.2), (1.21, .106, .086, .038, 2.2)]
    loft(pc, torso_rings(n, rs, .01), 'plate-dark')
    # Waist lames.
    for z, w in ((1.09, .104), (1.125, .101), (1.16, .102), (1.195, .107)):
        r = ring(V((0, .04, z)), V((1, 0, 0)), V((0, 1, 0)), w + .006, w * .82 + .006, n, 2.2, .012)
        loft(pc, [r, [p + V((0, 0, .018)) for p in r]], 'plate')
    return pc.make()


def faulds(tier):
    """The hip plates hanging from the waist, split at the front for the legs."""
    n = 24 if tier == 0 else 14
    pc = Piece('faulds')
    for k, (z, r0) in enumerate(((1.03, .128), (.99, .138), (.95, .146))):
        r = ring(V((0, .03, z)), V((1, 0, 0)), V((0, 1, 0)), r0, r0 * .78, n, 2.2)
        r2 = ring(V((0, .03, z - .05)), V((1, 0, 0)), V((0, 1, 0)), r0 + .012, (r0 + .012) * .8, n, 2.2)
        loft(pc, [r, r2], 'plate' if k != 1 else 'plate-trim')
    for side in (1, -1):
        for j, (x, y, ln, w) in enumerate(((.075, .12, .34, .06), (.135, .055, .3, .05))):
            blade_shape(pc, V((side * x, y, 1.0)), V((side * .08, .07, -1)).normalized(), V((1, 0, 0)), V((side * .15, 1, .05)).normalized(), ln, [w * .9, w, w * .96, w * .78, w * .44, .0], 'plate' if j == 0 else 'plate-trim')
    # The belt's clasp: a closed eye, as on the pommel.
    sphere(pc, V((0, .138, 1.035)), (.034, .012, .024), 'steel', u=14, v=8)
    tube(pc, [V((x, .15 - abs(x) * .1, 1.035 - .012 * (1 - (x / .03) ** 2))) for x in (-.03, -.015, 0, .015, .03)], .0022, 'steel-dark', segs=4)
    return pc.make()


def head_mask(tier, split=False):
    n = 28 if tier == 0 else 14
    pc = Piece('mask')
    # A long, narrow face: high brow, the ridge of the nose running to a pointed chin. No mouth.
    rs = [(1.555, .010, .012, .062, 2.0, .0), (1.575, .024, .026, .058, 2.0, .004), (1.6, .040, .046, .05, 2.1, .008), (1.635, .054, .064, .042, 2.2, .012), (1.67, .062, .074, .032, 2.3, .016), (1.705, .066, .08, .022, 2.3, .012), (1.74, .06, .074, .012, 2.2, .006), (1.772, .044, .054, .004, 2.1, .002), (1.79, .018, .022, .0, 2.0, .0)]
    rings = []
    for z, rx, ry, oy, pw, ridge in rs:
        r = ring(V((0, oy, z)), V((1, 0, 0)), V((0, 1, 0)), rx, ry, n, pw, ridge)
        if split:
            r = [V((p.x + (.009 if p.x > 0 else -.009), p.y, p.z)) for p in r]
        rings.append(r)
    loft(pc, rings, 'plate')
    # The brow's two sweeping lines and the crack down from the left eye.
    for side in (1, -1):
        b = [V((side * x, .112 - x * .36, 1.718 + x * .34)) for x in (.006, .02, .036, .052, .064)]
        tube(pc, gk.catmull(b, per=3), .0018, 'vein', segs=4)
    if not split:
        c = [V((-.03, .109, 1.665)), V((-.033, .108, 1.645)), V((-.027, .104, 1.625)), V((-.031, .098, 1.6))]
        tube(pc, gk.catmull(c, per=3), .0012, 'vein', segs=4)
    return pc.make()


def mask_eyes(tier):
    """The thin slits it sees through, faintly lit."""
    pc = Piece('mask-eyes')
    for side in (1, -1):
        pts = [V((side * x, .1125 - x * .38, 1.683 + x * .34)) for x in (.012, .024, .036, .048)]
        tube(pc, pts, .0028, 'light', segs=4, radii=[.0012, .0032, .0028, .0008])
    return pc.make()


def collar_fan(tier):
    """A high collar of long feather blades standing behind the head, an echo of the wings: on the chest's bone."""
    pc = Piece('fan')
    c = V((0, -.06, 1.47))
    count = 9 if tier == 0 else 5
    for k in range(count):
        s = k / (count - 1) * 2 - 1
        ang = s * 1.2
        way = V((math.sin(ang), -.32, math.cos(ang))).normalized()
        across = V((math.cos(ang), 0, -math.sin(ang)))
        thick = way.cross(across).normalized()
        ln = (.33 - .1 * abs(s)) * (1.04 if k % 2 == 0 else .9)
        blade_shape(pc, c + V((s * .07, 0, .0)), way, across, thick, ln, [.012, .03, .036, .03, .02, .008, .0], 'plate' if k % 2 == 0 else 'plate-trim')
    return pc.make()


def head_cowl(tier):
    n = 20 if tier == 0 else 12
    pc = Piece('cowl')
    rs = [(1.5, .1, .1, -.04), (1.54, .094, .1, -.045), (1.6, .092, .1, -.04), (1.68, .092, .1, -.03), (1.75, .08, .09, -.022), (1.795, .05, .058, -.018), (1.815, .018, .02, -.016)]
    loft(pc, [ring(V((0, oy, z)), V((1, 0, 0)), V((0, 1, 0)), rx, ry, n) for z, rx, ry, oy in rs], 'cloth')
    # The hood's edge, framing the mask.
    edge = [V((math.sin(a) * .088, .058 - .05 * math.cos(a) ** 2, 1.672 + math.cos(a) * .122)) for a in [i * math.pi * 2 / 22 for i in range(23)]]
    tube(pc, edge, .004, 'plate-trim', segs=5, closed=False)
    return pc.make()


def head_split_light(tier):
    pc = Piece('split-light')
    pts = [V((0, .115 - abs(z - 1.67) * .22, z)) for z in (1.56, 1.6, 1.64, 1.68, 1.72, 1.76)]
    tube(pc, pts, .0075, 'light', segs=5)
    return pc.make()


def halo(tier, shards=False):
    """On a chain at the head: a thin ring with one crack, or three orbiting shards. It stands up behind the head, axis Y."""
    pc = Piece('halo')
    c = V((0, -.14, 1.72))
    if not shards:
        torus(pc, c, V((0, 1, 0)), .17, .0055, 'light', segs=64 if tier == 0 else 32, rsegs=6)
        torus(pc, c, V((0, 1, 0)), .17, .0095, 'steel-dark', segs=48 if tier == 0 else 24, rsegs=6)
    return pc.make()


def halo_shard(i, tier):
    pc = Piece('halo-shard-%d' % i)
    c = V((0, -.14, 1.72))
    a0 = i * 2.1
    arc = .85
    pts = []
    for k in range(9):
        a = a0 + arc * k / 8
        pts.append(c + V((math.cos(a) * .17, 0, math.sin(a) * .17)))
    tube(pc, pts, .008, 'light', segs=6)
    return pc.make()


def pauldron(side, tier):
    n = 18 if tier == 0 else 10
    pc = Piece('pauldron-%d' % side)
    s = J('upperarm_r' if side > 0 else 'upperarm_l')
    e = J('lowerarm_r' if side > 0 else 'lowerarm_l')
    d = (e - s).normalized()
    # Three overlapping shells round the shoulder, each flaring up off the arm.
    for k, (t, r, f) in enumerate(((-.02, .098, .0), (.1, .086, .012), (.2, .074, .02))):
        c = s + d * (t * .25 + .0) + V((side * .012, 0, .018 - k * .006))
        rings = []
        for u, rr in ((-.06, .4), (-.03, .8), (.0, 1), (.03, 1.04), (.055, 1.0)):
            rings.append(ring(c + d * (u + .05 * k), V((0, 1, 0)), d.cross(V((0, 1, 0))).normalized(), r * rr, r * rr * .96, n))
        loft(pc, rings, 'plate' if k != 1 else 'plate-trim')
    # Coverts: long lames laid over the shoulder, overlapping like the wing's own feathers, the top one a crest.
    for k, (ang, ln, w) in enumerate(((.15, .2, .036), (-.25, .19, .036), (.55, .17, .032), (-.65, .165, .032), (.95, .14, .028), (-1.05, .135, .026))):
        way = (V((side * .78, 0, -.62)) + V((0, math.sin(ang), math.cos(ang) * .2))).normalized()
        base = s + V((side * .0, math.sin(ang) * .085, .083 * math.cos(ang) + .004))
        across = V((0, math.cos(ang), -math.sin(ang) * .5)).normalized()
        across = (across - way * across.dot(way)).normalized()
        thick = way.cross(across).normalized()
        if thick.z < 0:
            thick = -thick
        blade_shape(pc, base, way, across, thick, ln, [w * .6, w, w * .96, w * .72, w * .36, .0], 'plate' if k % 2 == 0 else 'plate-trim')
    tip = s + V((side * .03, -.01, .09))
    blade_shape(pc, tip, V((side * .86, -.12, .5)).normalized(), V((0, 1, 0)), V((side * -.5, 0, .86)).normalized(), .17, [.018, .028, .024, .014, .0], 'steel')
    return pc.make()


def rerebrace(side, tier):
    n = 14 if tier == 0 else 8
    pc = Piece('rerebrace-%d' % side)
    a, b = J('upperarm_r' if side > 0 else 'upperarm_l'), J('lowerarm_r' if side > 0 else 'lowerarm_l')
    limb(pc, a, b, [(.28, .05, .05), (.5, .052, .052), (.8, .047, .047), (.97, .045, .045)], 'plate', n, V((0, 1, 0)))
    sphere(pc, b, (.053, .053, .053), 'plate-trim', u=n, v=8)
    d = (b - a).normalized()
    out = V((side * .4, -.8, -.45)).normalized()
    blade_shape(pc, b + out * .03, out, d.cross(out).normalized(), d, .12, [.03, .024, .012, .0], 'steel')
    torus(pc, a + (b - a) * .3, d, .052, .004, 'plate-trim', segs=n + 4, rsegs=4)
    torus(pc, a + (b - a) * .82, d, .05, .004, 'plate-trim', segs=n + 4, rsegs=4)
    return pc.make()


def vambrace(side, tier):
    n = 14 if tier == 0 else 8
    pc = Piece('vambrace-%d' % side)
    a, b = J('lowerarm_r' if side > 0 else 'lowerarm_l'), J('hand_r' if side > 0 else 'hand_l')
    limb(pc, a, b, [(.06, .046, .046), (.35, .05, .05), (.75, .042, .042), (.96, .046, .046), (1.02, .05, .05)], 'plate', n, V((0, 1, 0)))
    d = (b - a).normalized()
    # A blade fin off the forearm's outside.
    blade_shape(pc, a + (b - a) * .25 + V((side * .03, -.02, .02)), d, V((side * .5, -.5, .6)).normalized(), V((.5, .7, -.3)).normalized(), .17, [.0, .018, .022, .016, .0], 'plate-trim')
    # The flared cuff.
    u = (V((0, 1, 0)) - d * V((0, 1, 0)).dot(d)).normalized()
    v = d.cross(u).normalized()
    rings = [ring(a + (b - a) * t, u, v, r, r, n) for t, r in ((.76, .043), (.9, .055), (.99, .068), (1.03, .07))]
    loft(pc, rings, 'plate-trim')
    return pc.make()


def gauntlet_right(tier):
    """The sword hand: a closed fist round the grip, in hand_r's own frame via the design rest."""
    pc = Piece('gauntlet-r')
    h = rest_matrix('hand_r')
    dm = M.inverted() @ h
    ax = (dm.to_3x3() @ V((1, 0, 0))).normalized()
    ay = (dm.to_3x3() @ V((0, 1, 0))).normalized()
    az = (dm.to_3x3() @ V((0, 0, 1))).normalized()
    o = dm.translation
    n = 12 if tier == 0 else 7
    # The back of the hand and the cuff.
    rings = [ring(o + ax * t, ay, az, ry, rz, n, 2.6) for t, ry, rz in ((-.01, .04, .03), (.03, .046, .03), (.075, .05, .03), (.105, .046, .028))]
    loft(pc, rings, 'plate')
    # Four fingers closed round the grip.
    centre = sword_in_design().translation
    bl = (sword_in_design().to_3x3() @ V((0, 0, 1))).normalized()
    for i in range(4):
        c = centre + bl * (-.03 + i * .022 - .012)
        torus(pc, c, bl, .03, .0115, 'plate-trim', segs=12 if tier == 0 else 8, rsegs=6)
    sphere(pc, centre + bl * .05 + ax * -.006 + az * .018, (.016, .016, .02), 'plate-trim', u=8, v=6)
    return pc.make()


def gauntlet_left(tier):
    """The empty hand: long, open, a claw."""
    pc = Piece('gauntlet-l')
    h = rest_matrix('hand_l')
    dm = M.inverted() @ h
    ax = (dm.to_3x3() @ V((1, 0, 0))).normalized()
    ay = (dm.to_3x3() @ V((0, 1, 0))).normalized()
    az = (dm.to_3x3() @ V((0, 0, 1))).normalized()
    o = dm.translation
    n = 12 if tier == 0 else 7
    rings = [ring(o + ax * t, ay, az, ry, rz, n, 2.6) for t, ry, rz in ((-.01, .04, .03), (.03, .045, .027), (.075, .048, .022), (.1, .044, .018))]
    loft(pc, rings, 'plate')
    for i in range(4):
        base = o + ax * .1 + ay * (-.033 + i * .022)
        tip = base + ax * .05 - az * .012
        tip2 = tip + ax * .045 - az * .028
        tip3 = tip2 + ax * .03 - az * .03
        tube(pc, [base, tip, tip2, tip3], .0085, 'plate-trim', segs=6, radii=[.0095, .0085, .007, .0025])
    base = o + ax * .035 + ay * .045
    tube(pc, [base, base + ax * .03 + ay * .025 - az * .012, base + ax * .06 + ay * .03 - az * .03], .009, 'plate-trim', segs=6, radii=[.01, .008, .003])
    return pc.make()


def cuisse(side, tier):
    n = 16 if tier == 0 else 9
    pc = Piece('cuisse-%d' % side)
    a, b = J('thigh_r' if side > 0 else 'thigh_l'), J('calf_r' if side > 0 else 'calf_l')
    limb(pc, a, b, [(.1, .082, .088, .004), (.4, .078, .082, .006), (.75, .064, .066, .006), (.94, .058, .06, .004)], 'plate', n, V((1, 0, 0)), keel=.008)
    # The knee, pointed.
    sphere(pc, b + V((0, .018, 0)), (.058, .056, .06), 'plate-trim', u=n, v=8)
    blade_shape(pc, b + V((0, .05, .06)), V((0, .3, -1)).normalized(), V((1, 0, 0)), V((0, 1, .2)).normalized(), .1, [.02, .03, .02, .0], 'plate')
    return pc.make()


def greave(side, tier):
    n = 16 if tier == 0 else 9
    pc = Piece('greave-%d' % side)
    a, b = J('calf_r' if side > 0 else 'calf_l'), J('foot_r' if side > 0 else 'foot_l')
    limb(pc, a, b, [(.05, .052, .058, .004), (.3, .058, .066, .002), (.65, .046, .052, .0), (.96, .04, .046, .0)], 'plate', n, V((1, 0, 0)), keel=.012)
    d = (b - a).normalized()
    blade_shape(pc, a + (b - a) * .08 + V((0, .058, 0)), d, V((1, 0, 0)), V((0, 1, 0)), .24, [.0, .008, .012, .009, .0], 'plate-trim')
    torus(pc, a + (b - a) * .92, d, .048, .004, 'plate-trim', segs=n + 4, rsegs=4)
    return pc.make()


def sabaton(side, tier):
    n = 12 if tier == 0 else 8
    pc = Piece('sabaton-%d' % side)
    f, b = J('foot_r' if side > 0 else 'foot_l'), J('ball_r' if side > 0 else 'ball_l')
    heel = f + V((0, -.05, -.07))
    toe = V((b.x, b.y + .13, .022))
    rings = [
        ring(heel + V((0, 0, .04)), V((1, 0, 0)), V((0, 0, 1)), .036, .04, n, 2.4),
        ring(f + V((0, .03, -.03)), V((1, 0, 0)), V((0, 0, 1)), .046, .06, n, 2.4),
        ring(b + V((0, -.01, .012)), V((1, 0, 0)), V((0, 0, 1)), .044, .032, n, 2.4),
        ring(b + V((0, .07, .012)), V((1, 0, 0)), V((0, 0, 1)), .028, .02, n, 2.4),
        ring(toe + V((0, 0, .004)), V((1, 0, 0)), V((0, 0, 1)), .006, .006, n, 2.0),
    ]
    loft(pc, rings, 'plate')
    return pc.make()


# The robe: strips round the waist the game swings. Each segment is its own chain link: (angle round the waist in degrees from the front, half width).
ROBE = ((-22, .085), (22, .085), (-82, .08), (82, .08), (-140, .1), (140, .1), (180, .11))
ROBE_LEN = (.3, .3, .32)
ROBE_Z = .955


def robe_pivot(i, seg):
    a = math.radians(ROBE[i][0])
    r = .14
    x, y = math.sin(a) * r, .03 + math.cos(a) * r * .8
    o = V((x, y, ROBE_Z))
    out = V((math.sin(a), math.cos(a), 0))
    for k in range(seg):
        o = o + V((0, 0, -ROBE_LEN[k])) + out * .03
    return o


def robe_seg(i, seg, tier, burnt=0.0):
    """One strip's segment. `burnt` eats it upward (phase 2's robe burning away)."""
    a = math.radians(ROBE[i][0])
    w = ROBE[i][1] * (1 - .13 * seg)
    p0 = robe_pivot(i, seg)
    out = V((math.sin(a), math.cos(a), 0))
    across = V((math.cos(a), -math.sin(a), 0))
    pc = Piece('robe-%d-%d' % (i, seg))
    ln = ROBE_LEN[seg]
    cols = 5 if tier == 0 else 3
    rows = 5 if tier == 0 else 3
    r = rnd(i * 31 + seg * 7)
    bm = pc.bm
    idx = pc.slot('cloth')
    edge = pc.slot('cloth-edge')
    grid = []
    for row in range(rows + 1):
        t = row / rows
        line = []
        for c in range(cols + 1):
            s = c / cols * 2 - 1
            fold = math.sin(s * 3.0 + i) * .012
            drop = t * ln
            if seg == len(ROBE_LEN) - 1 and row == rows:
                drop += r.uniform(-.07, .05) * (1 if tier == 0 else .6)
            if burnt > 0 and row == rows:
                drop -= r.uniform(0, burnt)
            line.append(bm.verts.new(tuple(p0 + across * (s * w * (1 - .08 * t)) + out * (.03 * t + fold) + V((0, 0, -drop)))))
        grid.append(line)
    for row in range(rows):
        for c in range(cols):
            f = bm.faces.new((grid[row][c], grid[row][c + 1], grid[row + 1][c + 1], grid[row + 1][c]))
            f.material_index = edge if (c == 0 or c == cols - 1 or (seg == len(ROBE_LEN) - 1 and row == rows - 1)) else idx
    pc.solidify(.006, 0.0)
    return pc.make(recalc=True)


# ---------------------------------------------------------------------------------------------------------------------------
# The wings: six, three segments each. Modelled spread, on chains the game folds. side +1 right, -1 left.
# ---------------------------------------------------------------------------------------------------------------------------
WINGS = (
    dict(root=(.05, -.1, 1.42), way=(1, -.25, .62), seg=(.34, .44, .48), feather=(.3, .44, .62), count=(5, 9, 10)),
    dict(root=(.055, -.11, 1.33), way=(1, -.3, .14), seg=(.3, .38, .42), feather=(.26, .38, .52), count=(4, 8, 9)),
    dict(root=(.05, -.1, 1.24), way=(1, -.3, -.34), seg=(.26, .32, .36), feather=(.22, .32, .44), count=(4, 7, 8)),
)


def wing_points(w, side):
    bend = (0.0, -.42, -.9)
    p = V(w['root'])
    p = V((p.x * side, p.y, p.z))
    pts = [p]
    way = V((w['way'][0] * side, w['way'][1], w['way'][2])).normalized()
    for k, ln in enumerate(w['seg']):
        d = way.copy()
        # Each segment bends a little further down.
        d = (d + V((0, 0, bend[k] * .6))).normalized()
        p = p + d * ln
        pts.append(p)
    return pts


def feather(pc, base, way, across, length, width, fin, tier, curve=.08):
    n = 5 if tier == 0 else 3
    widths = [width * x for x in ((.3, .85, 1, .72, .0) if n == 5 else (.5, 1, .0))]
    normal = across.cross(way).normalized()
    # A curved leaf: built in short straight pieces with blade_shape's section.
    bm = pc.bm
    idx = pc.slot(fin)
    rows = []
    for i, wd in enumerate(widths):
        t = i / (len(widths) - 1)
        c = base + way * (length * t) + normal * (curve * length * t * t)
        rows.append([bm.verts.new(tuple(c + across * wd)), bm.verts.new(tuple(c + normal * (wd * .1 + .0004))), bm.verts.new(tuple(c - across * wd)), bm.verts.new(tuple(c - normal * (wd * .1 + .0004)))])
    for a, b in zip(rows, rows[1:]):
        for i in range(4):
            j = (i + 1) % 4
            bm.faces.new((a[i], a[j], b[j], b[i])).material_index = idx


def wing_seg(wi, side, seg, tier, form='feather'):
    """One wing segment: its arm, and its feathers. form: feather (whole), shed (half gone), bone (bare), blade (bare with one huge blade on the end)."""
    w = WINGS[wi]
    pts = wing_points(w, side)
    a, b = pts[seg], pts[seg + 1]
    d = (b - a).normalized()
    pc = Piece('wing-%d-%d-%d-%s' % (wi, side, seg, form))
    r0, r1 = (.026, .018, .012)[seg], (.018, .012, .005)[seg]
    cyl(pc, a, b, r0, 'bone' if form in ('bone', 'blade') else 'plate-trim', segs=8 if tier == 0 else 5, r1=r1)
    sphere(pc, a, (r0 * 1.15, r0 * 1.15, r0 * 1.15), 'bone' if form in ('bone', 'blade') else 'plate-trim', u=8, v=6)
    trailing = V((.25 * side, -.25, -1)).normalized()
    trailing = (trailing - d * trailing.dot(d)).normalized()
    normal = d.cross(trailing).normalized()
    if form in ('feather', 'shed'):
        count = w['count'][seg]
        rg = rnd(wi * 100 + seg * 10 + (side > 0))
        for k in range(count):
            if form == 'shed' and rg.random() < .55:
                continue
            t = (k + .35) / count
            base = a + (b - a) * t
            # The fan opens toward the tip on the outermost segment.
            fan = (t * .95 if seg == 2 else t * .25 if seg == 1 else 0)
            way = (trailing * (1 - fan) + d * fan).normalized()
            across = d.cross(way).cross(way).normalized() * -1
            across = (d - way * d.dot(way)).normalized()
            ln = w['feather'][seg] * (1.05 - .25 * abs(t - .55)) * rg.uniform(.94, 1.04)
            feather(pc, base, way, across, ln, .032 + .012 * seg, 'feather', tier)
            if tier == 0:
                # The covert over each: shorter and darker.
                feather(pc, base + normal * .006, way, across, ln * .52, (.032 + .012 * seg) * .9, 'feather-dark', tier, curve=.04)
    if form == 'blade' and seg == 2:
        blade_shape(pc, b - d * .02, (d + V((0, 0, .2))).normalized(), normal, trailing, 1.05, [.03, .07, .085, .075, .05, .022, .0], 'steel-dark')
    return pc.make()


# The ring of black blades behind it (phase 4), one baked blade the game spins round a pivot.
BLADE_RING_PIVOT = (0, -.2, 1.38)


def ring_blade(tier):
    pc = Piece('ring-blade')
    p = V(BLADE_RING_PIVOT)
    blade_shape(pc, p + V((.22, 0, 0)), V((1, 0, 0)), V((0, 0, 1)), V((0, 1, 0)), .68, [.02, .05, .06, .05, .034, .014, .0], 'steel-dark')
    return pc.make()


# ---------------------------------------------------------------------------------------------------------------------------
# The claymore: in the sword's own frame (X across the edges, Y through the flats, Z up the blade, the fist's centre at the origin).
# About 3.4 m as the game draws it, so 1.8 here. Longer than the wielder is tall.
# ---------------------------------------------------------------------------------------------------------------------------
BLADE_FROM, BLADE_LEN, BLADE_HALF = .15, 1.5, .068


def blade_width(t):
    # A slight waist below a long, leaf taper to the point.
    w = BLADE_HALF * (1.0 - .12 * math.sin(min(1, t / .55) * math.pi) * .6)
    if t > .78:
        w *= max(0.0, 1 - ((t - .78) / .22) ** 1.5)
    return w


def sword_blade(tier, fin, edge_fin):
    n = 26 if tier == 0 else 12
    pc = Piece('blade')
    bm = pc.bm
    i_f = pc.slot(fin)
    i_e = pc.slot(edge_fin)
    rows = []
    for k in range(n + 1):
        t = k / n
        z = BLADE_FROM + BLADE_LEN * t
        w = blade_width(t)
        rows.append([bm.verts.new((w, 0, z)), bm.verts.new((w * .72, .0035 + w * .1, z)), bm.verts.new((w * .24, .012 * (1 - t * .6), z)), bm.verts.new((0, .007 * (1 - t * .6), z)),
                     bm.verts.new((-w * .24, .012 * (1 - t * .6), z)), bm.verts.new((-w * .72, .0035 + w * .1, z)), bm.verts.new((-w, 0, z)), bm.verts.new((-w * .72, -.0035 - w * .1, z)),
                     bm.verts.new((-w * .24, -.012 * (1 - t * .6), z)), bm.verts.new((0, -.007 * (1 - t * .6), z)), bm.verts.new((w * .24, -.012 * (1 - t * .6), z)), bm.verts.new((w * .72, -.0035 - w * .1, z))])
    mats = [i_e, i_f, i_f, i_f, i_f, i_e, i_e, i_f, i_f, i_f, i_f, i_e]
    for a, b in zip(rows, rows[1:]):
        for i in range(12):
            j = (i + 1) % 12
            bm.faces.new((a[i], a[j], b[j], b[i])).material_index = mats[i]
    bm.faces.new(rows[0][::-1]).material_index = i_f
    return pc.make()


def sword_hilt(tier):
    pc = Piece('hilt')
    n = 12 if tier == 0 else 7
    # The grip, wrapped, and its collar.
    cyl(pc, V((0, 0, -.12)), V((0, 0, .1)), .019, 'black', segs=n, r1=.017)
    for k in range(12 if tier == 0 else 5):
        z = -.11 + k * .2 / (12 if tier == 0 else 5)
        torus(pc, V((0, 0, z)), V((0, 0, 1)), .019, .0032, 'steel-dark', segs=n, rsegs=4)
    # The crossguard: a pair of broken wings folding forward (up the blade).
    for side in (1, -1):
        for k in range(5):
            base = V((side * (.015 + k * .028), 0, .112 + k * .004))
            way = V((side * (.82 - k * .1), 0, .38 + k * .18)).normalized()
            ln = .23 - k * .03
            blade_shape(pc, base, way, V((0, 1, 0)).cross(way).normalized(), V((0, 1, 0)), ln, [.012, .026, .028, .018, .0], 'steel' if k % 2 == 0 else 'plate-trim')
    # The guard's boss.
    sphere(pc, V((0, 0, .118)), (.042, .022, .024), 'steel', u=n, v=7)
    # The ricasso, wrapped in chain.
    cyl(pc, V((0, 0, .13)), V((0, 0, .3)), .012, 'steel-dark', segs=8, r1=.012, caps=False)
    if tier == 0:
        for k in range(10):
            z = .15 + k * .016
            a = k * 1.6
            torus(pc, V((math.cos(a) * .058, math.sin(a) * .018, z)), V((math.sin(a), math.cos(a), .3)).normalized(), .012, .0028, 'chain', segs=10, rsegs=4)
    # The pommel: a closed eye.
    sphere(pc, V((0, 0, -.15)), (.03, .024, .036), 'steel', u=n, v=8)
    torus(pc, V((0, .0, -.15)), V((0, 1, 0)), .03, .0045, 'steel-dark', segs=n + 4, rsegs=4)
    return pc.make()


def pommel_eye(tier):
    pc = Piece('eye')
    sphere(pc, V((0, .022, -.15)), (.012, .006, .012), 'light', u=8, v=6)
    sphere(pc, V((0, -.022, -.15)), (.012, .006, .012), 'light', u=8, v=6)
    return pc.make()


def sword_veins(tier, reach, seed):
    """The black veins climbing the blade from the guard, to `reach` of its length."""
    pc = Piece('veins-%d' % seed)
    r = rnd(seed)
    count = 10 if tier == 0 else 5
    for k in range(count):
        for face in (1, -1):
            x = r.uniform(-.75, .75)
            z = BLADE_FROM - .01
            top = BLADE_FROM + BLADE_LEN * reach * r.uniform(.55, 1)
            pts = []
            while z < top:
                t = (z - BLADE_FROM) / BLADE_LEN
                w = blade_width(max(0, t))
                y = face * (.0125 * (1 - .6 * t) + .001)
                pts.append(V((x * w, y, z)))
                z += r.uniform(.05, .1)
                x = max(-.95, min(.95, x + r.uniform(-.28, .28)))
            if len(pts) > 1:
                tube(pc, pts, .0028 * (1 + reach), 'vein', segs=4, caps=False)
    return pc.make()


def sword_words(tier):
    """LUX CECIDIT, cut into the fuller."""
    pc_obs = []
    for face in (1, -1):
        cu = bpy.data.curves.new('words', 'FONT')
        cu.body = 'LUX CECIDIT'
        cu.size = .034
        cu.extrude = .0008
        ob = bpy.data.objects.new('words', cu)
        bpy.context.scene.collection.objects.link(ob)
        ob.rotation_euler = (math.pi / 2 * face, 0, math.pi / 2 * face)
        ob.location = (-.012 * face if False else 0, face * .0145, BLADE_FROM + .1 if face > 0 else BLADE_FROM + .1)
        bpy.context.view_layer.update()
        deps = bpy.context.evaluated_depsgraph_get()
        me = bpy.data.meshes.new_from_object(ob.evaluated_get(deps))
        me.transform(ob.matrix_world)
        # The sword's frame is a mirror of its design: the words are cut backwards here so they read forwards on the blade.
        zs = [v.co.z for v in me.vertices]
        zc = (min(zs) + max(zs)) * .5
        for v in me.vertices:
            v.co.z = 2 * zc - v.co.z
        me.flip_normals()
        bpy.data.objects.remove(ob)
        out = bpy.data.objects.new('words-mesh', me)
        bpy.context.scene.collection.objects.link(out)
        me.materials.append(bpy.data.materials['vein'])
        pc_obs.append(out)
    return pc_obs


def sword_light_blade(tier):
    """Phase 4: a shard of pure white light in the blade's shape, the black steel flaking off it."""
    return sword_blade(tier, 'light', 'light')


def sword_flakes(tier):
    pc = Piece('flakes')
    r = rnd(44)
    for k in range(40 if tier == 0 else 16):
        t = r.uniform(.02, .95)
        z = BLADE_FROM + BLADE_LEN * t
        w = blade_width(t)
        x = r.uniform(-1.1, 1.1) * w
        y = r.choice((1, -1)) * r.uniform(.012, .05)
        blade_shape(pc, V((x, y, z)), V((r.uniform(-.4, .4), r.uniform(-.3, .3), 1)).normalized(), V((1, 0, 0)), V((0, 1, 0)), r.uniform(.02, .07), [r.uniform(.006, .018), r.uniform(.006, .02), .0], 'black')
    return pc.make()


# ---------------------------------------------------------------------------------------------------------------------------
# What isn't on the body: the one feather of the cutscene, and the three adds. In metres as drawn, origin on the water under them.
# ---------------------------------------------------------------------------------------------------------------------------
def one_feather(tier):
    pc = Piece('one-feather')
    feather(pc, V((0, 0, 0)), V((0, 0, 1)), V((1, 0, 0)), .42, .05, 'light', 0, curve=.12)
    cyl(pc, V((0, 0, -.06)), V((0, .004, .3)), .0035, 'light', segs=5, r1=.001)
    return pc.make()


def cantor(tier):
    """A hollow white robe hanging in the air with no one inside, its open hood singing."""
    n = 18 if tier == 0 else 10
    pc = Piece('cantor')
    x, y, z = V((1, 0, 0)), V((0, 1, 0)), V((0, 0, 1))
    # The robe: a bell, narrow at the shoulders, its hem torn.
    rings = []
    for zz, rr, ry in ((2.32, .05, .05), (2.2, .24, .17), (2.0, .3, .2), (1.6, .34, .26), (1.1, .44, .36), (.55, .58, .5), (.2, .66, .58)):
        rings.append(ring(V((0, -.02, zz)), x, y, rr, ry, n, 2.0))
    loft(pc, rings, 'feather')
    r = rnd(71)
    # The tears: long strips below the hem.
    for k in range(9 if tier == 0 else 5):
        a = k / (9 if tier == 0 else 5) * 2 * math.pi + r.uniform(-.2, .2)
        base = V((math.cos(a) * .6, math.sin(a) * .52, .26))
        pts = [base, base + V((math.cos(a) * .05, math.sin(a) * .05, -.2)), base + V((math.cos(a) * .03, math.sin(a) * .03, -r.uniform(.35, .7)))]
        tube(pc, pts, .05, 'feather-dark', segs=4, caps=True)
    # The hood, open forward over nothing.
    hood = [ring(V((0, .02 + .01 * i, 2.22 + i * .11)), x, y, rr, ry, n, 2.0) for i, (rr, ry) in enumerate(((.2, .2), (.24, .24), (.25, .25), (.21, .22), (.12, .13)))]
    loft(pc, hood, 'feather')
    # The mouth: the black inside, its rim, and the white of the note deep in it.
    sphere(pc, V((0, .17, 2.46)), (.15, .08, .19), 'black', u=n, v=8)
    torus(pc, V((0, .19, 2.46)), V((0, 1, 0)), .16, .018, 'plate-trim', segs=n, rsegs=5)
    sphere(pc, V((0, .22, 2.46)), (.03, .02, .05), 'light', u=8, v=6)
    # Sleeves that end in nothing, held out.
    for side in (1, -1):
        pts = [V((side * .25, 0, 2.0)), V((side * .62, .1, 1.72)), V((side * .78, .22, 1.34))]
        tube(pc, pts, .1, 'feather', segs=8 if tier == 0 else 5, caps=True)
        sphere(pc, V((side * .8, .24, 1.3)), (.085, .085, .05), 'black', u=8, v=5)
    return pc.make()


def penitent(tier):
    """A pale husk on its knees, bent over, arms reaching."""
    n = 14 if tier == 0 else 8
    pc = Piece('penitent')
    x, y = V((1, 0, 0)), V((0, 1, 0))
    spine = [(.0, .3, .13, .1), (.1, .6, .17, .12), (.26, .88, .21, .14), (.45, 1.02, .19, .13), (.58, 1.08, .09, .08)]
    rings = []
    for yy, zz, rx, ry in spine:
        rings.append(ring(V((0, yy, zz)), x, V((0, 1, -1.2 * (yy + .1))).normalized(), rx, ry, n, 2.2, spine=.03))
    loft(pc, rings, 'bone')
    # The ribs, standing off the back.
    for k in range(5 if tier == 0 else 2):
        zz = .55 + k * .09
        yy = .03 + k * .08
        for side in (1, -1):
            tube(pc, [V((side * .03, yy - .1, zz + .02)), V((side * .15, yy - .05, zz)), V((side * .19, yy + .08, zz - .05))], .012, 'plate-dark', segs=4, caps=False)
    # The bowed head, no face.
    sphere(pc, V((0, .74, 1.0)), (.105, .135, .125), 'bone', u=n, v=9)
    sphere(pc, V((0, .84, .96)), (.05, .03, .06), 'black', u=8, v=5)
    # The arms reaching to the water.
    for side in (1, -1):
        tube(pc, [V((side * .2, .44, .96)), V((side * .34, .7, .62)), V((side * .26, 1.02, .28)), V((side * .22, 1.2, .03))], .045, 'bone', segs=7 if tier == 0 else 5, caps=True)
        for f in (-1, 0, 1):
            tube(pc, [V((side * .22, 1.2, .03)), V((side * .22 + f * .05, 1.3, .015))], .012, 'bone', segs=4, caps=True)
    # The thighs folded under it.
    for side in (1, -1):
        tube(pc, [V((side * .12, .0, .32)), V((side * .2, .32, .14))], .085, 'bone', segs=8 if tier == 0 else 5, caps=True)
        tube(pc, [V((side * .2, .32, .1)), V((side * .16, -.14, .05))], .06, 'bone', segs=7 if tier == 0 else 5, caps=True)
    return pc.make()


def throne_ring(tier):
    """Black blades round a hole, the eye's place: the game turns it. In the plane of X and Z, blades pointing out."""
    pc = Piece('throne-ring')
    count = 11
    for k in range(count):
        a = k / count * 2 * math.pi
        out = V((math.cos(a), 0, math.sin(a)))
        tan = V((-math.sin(a), 0, math.cos(a)))
        blade_shape(pc, out * .62, (out + tan * .35).normalized(), V((0, 1, 0)), tan, 1.5, [.03, .1, .13, .11, .07, .03, .0], 'steel-dark')
    torus(pc, V((0, 0, 0)), V((0, 1, 0)), .66, .05, 'steel-dark', segs=28 if tier == 0 else 16, rsegs=6)
    torus(pc, V((0, 0, 0)), V((0, 1, 0)), .78, .014, 'plate-trim', segs=28 if tier == 0 else 16, rsegs=4)
    return pc.make()


def throne_eye(tier):
    """One white eye that looks at you. Forward is +Y."""
    pc = Piece('throne-eye')
    sphere(pc, V((0, 0, 0)), (.5, .5, .5), 'plate', u=20 if tier == 0 else 10, v=14 if tier == 0 else 8)
    torus(pc, V((0, .36, 0)), V((0, 1, 0)), .3, .018, 'light', segs=22 if tier == 0 else 12, rsegs=4)
    sphere(pc, V((0, .42, 0)), (.2, .1, .2), 'black', u=14 if tier == 0 else 8, v=8)
    sphere(pc, V((0, .5, 0)), (.05, .025, .05), 'light', u=8, v=5)
    return pc.make()


# ---------------------------------------------------------------------------------------------------------------------------
# The parts: name, how it's baked. A bone name, or a chain: wing<pair><r|l>-<seg>, robe<i>-<seg>, halo, blade<i>. Core NativeFallenBossLook.Parts says the phases.
# ---------------------------------------------------------------------------------------------------------------------------
def build(tier):
    """(prop, group, objects, to_frame) for everything."""
    out = []

    def add(name, group, obs, frame):
        out.append((name, group, obs if isinstance(obs, list) else [obs], frame))

    for bone, ob in undersuit(tier).items():
        add('under-' + bone, bone, ob, to_bone(bone))
    for bone, ob in core_body(tier).items():
        add('core-' + bone, bone, ob, to_bone(bone))
    add('head-light', 'head', head_light(tier), to_bone('head'))
    add('cuirass', 'spine_05', cuirass(tier), to_bone('spine_05'))
    add('cuirass-broken', 'spine_05', cuirass(tier, True), to_bone('spine_05'))
    add('glass', 'spine_05', chest_glass(tier), to_bone('spine_05'))
    add('heart', 'spine_05', chest_light(tier), to_bone('spine_05'))
    add('cracks-chest', 'spine_05', cracks(tier, 5, [((.03, .12, 1.42), (.3, .05, -1), .2), ((-.05, .125, 1.4), (-.5, .02, -1), .18), ((.1, .1, 1.36), (.7, -.2, -.7), .1), ((.0, .13, 1.3), (.2, -.05, -1), .1)]), to_bone('spine_05'))
    add('abdomen', 'spine_02', abdomen(tier), to_bone('spine_02'))
    add('faulds', 'pelvis', faulds(tier), to_bone('pelvis'))
    add('cowl', 'head', head_cowl(tier), to_bone('head'))
    add('mask', 'head', head_mask(tier), to_bone('head'))
    add('mask-split', 'head', head_mask(tier, True), to_bone('head'))
    add('mask-eyes', 'head', mask_eyes(tier), to_bone('head'))
    add('fan', 'spine_05', collar_fan(tier), to_bone('spine_05'))
    add('split-light', 'head', head_split_light(tier), to_bone('head'))
    add('halo', 'halo', halo(tier), to_pivot((0, -.14, 1.72)))
    for i in range(3):
        add('halo-shard-%d' % i, 'halo', halo_shard(i, tier), to_pivot((0, -.14, 1.72)))
    add('pauldron-r', 'upperarm_r', pauldron(1, tier), to_bone('upperarm_r'))
    add('pauldron-l', 'upperarm_l', pauldron(-1, tier), to_bone('upperarm_l'))
    add('rerebrace-r', 'upperarm_r', rerebrace(1, tier), to_bone('upperarm_r'))
    add('rerebrace-l', 'upperarm_l', rerebrace(-1, tier), to_bone('upperarm_l'))
    add('vambrace-r', 'lowerarm_r', vambrace(1, tier), to_bone('lowerarm_r'))
    add('vambrace-l', 'lowerarm_l', vambrace(-1, tier), to_bone('lowerarm_l'))
    add('gauntlet-r', 'hand_r', gauntlet_right(tier), to_bone('hand_r'))
    add('gauntlet-l', 'hand_l', gauntlet_left(tier), to_bone('hand_l'))
    for s, tag in ((1, 'r'), (-1, 'l')):
        add('cuisse-' + tag, 'thigh_' + tag, cuisse(s, tier), to_bone('thigh_' + tag))
        add('greave-' + tag, 'calf_' + tag, greave(s, tier), to_bone('calf_' + tag))
        add('sabaton-' + tag, 'foot_' + tag, sabaton(s, tier), to_bone('foot_' + tag))
    for i in range(len(ROBE)):
        for seg in range(len(ROBE_LEN)):
            add('robe%d-%d' % (i, seg), 'robe%d-%d' % (i, seg), robe_seg(i, seg, tier), to_pivot(robe_pivot(i, seg)))
            if seg == len(ROBE_LEN) - 1:
                add('robe%d-%d-burnt' % (i, seg), 'robe%d-%d' % (i, seg), robe_seg(i, seg, tier, .22), to_pivot(robe_pivot(i, seg)))
    for wi in range(len(WINGS)):
        for side, tag in ((1, 'r'), (-1, 'l')):
            pts = wing_points(WINGS[wi], side)
            for seg in range(3):
                frame = to_pivot(pts[seg])
                group = 'wing%d%s-%d' % (wi, tag, seg)
                add(group, group, wing_seg(wi, side, seg, tier, 'feather'), frame)
                add(group + '-shed', group, wing_seg(wi, side, seg, tier, 'shed'), frame)
                add(group + '-bone', group, wing_seg(wi, side, seg, tier, 'blade' if wi == 0 else 'bone'), frame)
    add('ring-blade', 'blade', ring_blade(tier), to_pivot(BLADE_RING_PIVOT))
    sw = to_bone_sword()
    add('sword-blade', 'hand_r', sword_blade(tier, 'steel', 'plate'), sw)
    add('sword-blade-dark', 'hand_r', sword_blade(tier, 'black', 'light'), sw)
    add('sword-blade-light', 'hand_r', sword_light_blade(tier), sw)
    add('sword-hilt', 'hand_r', sword_hilt(tier), sw)
    add('sword-eye', 'hand_r', pommel_eye(tier), sw)
    add('sword-words', 'hand_r', sword_words(tier), sw)
    add('sword-veins-1', 'hand_r', sword_veins(tier, .12, 1), sw)
    add('sword-veins-2', 'hand_r', sword_veins(tier, .55, 2), sw)
    add('sword-flakes', 'hand_r', sword_flakes(tier), sw)
    free = to_pivot((0, 0, 0))
    add('feather-one', 'free', one_feather(tier), free)
    add('cantor', 'free', cantor(tier), free)
    add('penitent', 'free', penitent(tier), free)
    add('throne-ring', 'free', throne_ring(tier), free)
    add('throne-eye', 'free', throne_eye(tier), free)
    return out


def to_bone_sword():
    return to_sword()


# Which parts show in each phase (mirrors Core NativeFallenBossLook.Parts, for the sheet).
def shown(name, phase):
    if name in ('feather-one', 'cantor', 'penitent', 'throne-ring', 'throne-eye'):
        return False
    if name.startswith('under-') or name == 'cowl':
        return phase <= 3
    if name.startswith('core-') or name == 'head-light':
        return phase == 4
    if name in ('abdomen',) or name.startswith(('rerebrace', 'vambrace', 'cuisse', 'greave')):
        return phase <= 3
    if name in ('faulds', 'gauntlet-r', 'gauntlet-l') or name.startswith(('pauldron', 'sabaton')):
        return True
    if name == 'cuirass':
        return phase <= 2
    if name == 'cuirass-broken' or name == 'glass' or name == 'heart':
        return phase >= 3
    if name == 'cracks-chest':
        return phase == 2
    if name in ('mask', 'mask-eyes', 'fan'):
        return phase <= 2
    if name in ('mask-split', 'split-light'):
        return phase == 3
    if name == 'halo':
        return phase <= 2
    if name.startswith('halo-shard'):
        return phase == 3
    if name.startswith('robe'):
        burnt = name.endswith('-burnt')
        last = name.split('-')[1] == str(len(ROBE_LEN) - 1)
        if phase == 1:
            return not burnt
        if phase == 2:
            return (burnt if last else True)
        return False if last else (name.split('-')[1] == '0')
    if name.startswith('wing'):
        pair = int(name[4])
        kind = name.split('-')[2] if name.count('-') >= 2 else 'feather'
        if phase == 1:
            return kind == 'feather'
        if phase == 2:
            return kind == ('shed' if pair == 2 else 'feather')
        if phase == 3:
            return kind == 'bone'
        return False
    if name == 'ring-blade':
        return phase == 4
    if name == 'sword-blade':
        return phase <= 2
    if name == 'sword-blade-dark':
        return phase == 3
    if name == 'sword-blade-light':
        return phase == 4
    if name == 'sword-words':
        return phase <= 2
    if name == 'sword-hilt':
        return True
    if name == 'sword-eye':
        return phase >= 3
    if name == 'sword-veins-1':
        return phase == 1
    if name == 'sword-veins-2':
        return phase == 2
    if name == 'sword-flakes':
        return phase == 4
    return True


def sheet(parts, out_dir, tier):
    """Each phase assembled at rest, from the front, three quarters and behind, in the bank's own colours."""
    os.makedirs(out_dir, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'VERTEX'
    scene.display.shading.show_cavity = True
    scene.render.resolution_x, scene.render.resolution_y = 720, 900
    scene.render.film_transparent = False
    scene.world = bpy.data.worlds.new('w')
    scene.world.color = (.04, .04, .05)
    scene.display.render_aa = 'FXAA'
    cam = bpy.data.cameras.new('c')
    cam.type = 'ORTHO'
    cam.ortho_scale = 3.3
    co = bpy.data.objects.new('c', cam)
    scene.collection.objects.link(co)
    scene.camera = co
    for phase in (1, 2, 3, 4):
        mine = []
        for name, gm, frame in parts:
            if not shown(name, phase):
                continue
            inv = to_bone('hand_r').inverted() if name.startswith('sword') else frame.inverted()
            flip = frame.to_3x3().determinant() < 0
            me = bpy.data.meshes.new('s-' + name)
            verts = [tuple(inv @ V(p)) for p in gm.positions]
            tris = []
            for sub in gm.submeshes:
                for i in range(0, len(sub), 3):
                    t = sub[i:i + 3]
                    tris.append((t[0], t[2], t[1]) if flip else tuple(t))
            me.from_pydata(verts, [], tris)
            attr = me.color_attributes.new('Col', 'BYTE_COLOR', 'POINT')
            for i, c in enumerate(gm.colors):
                attr.data[i].color_srgb = (c[0], c[1], c[2], 1.0)
            ob = bpy.data.objects.new('s-' + name, me)
            scene.collection.objects.link(ob)
            mine.append(ob)
        for yaw, tag in ((0, 'front'), (38, 'tq'), (180, 'back')):
            a = math.radians(yaw)
            co.location = (math.sin(a) * 6, math.cos(a) * 6, 1.25)
            co.rotation_euler = (math.pi / 2, 0, math.pi + a * -1)
            scene.render.filepath = os.path.join(out_dir, 'p%d_%s.png' % (phase, tag))
            bpy.ops.render.render(write_still=True)
        for ob in mine:
            bpy.data.objects.remove(ob)


def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument('--out', help='Assets/LocalAssets to write the bank into')
    ap.add_argument('--sheet', help='render the review sheets of tier 0')
    ap.add_argument('--tiers', default='0,1')
    args = ap.parse_args(argv)
    finishes()
    bank = gk.GearBank()
    keep = []
    for tier in [int(x) for x in args.tiers.split(',')]:
        gk.clear_scene()
        made = build(tier)
        total = 0
        for name, group, obs, frame in made:
            gm = gk.bake('fb-%s-%d' % (name, tier), obs, frame, seed=sum(ord(c) * (i + 1) for i, c in enumerate(name)) % 97 / 97.0)
            if not gm.positions:
                print('empty', name)
                continue
            bank.add('fb-%s-%d' % (name, tier), group, gm)
            total += sum(len(s) for s in gm.submeshes) // 3
            if tier == 0:
                keep.append((name, gm, frame))
        print('tier %d: %d triangles in the bank' % (tier, total))
    if args.sheet:
        gk.clear_scene()
        sheet(keep, args.sheet, 0)
    if args.out:
        bank.write(args.out, BANK)


if __name__ == '__main__':
    main()
