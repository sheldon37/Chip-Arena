"""THE CONVERTER rebuilt part by part in Blender (docs/EXPEDITION-ENEMY-REWORK.md section 2, docs/EXPEDITION-BOSS-CITY-IRONWORKS.md "Presentation build"): an 8 m Bessemer vessel in its yoke
on four crab legs, the dead foreman at its levers. Every prop is one joint of Core NativeIronworksBossLook, in that joint's rest frame and at the numbers the code sculpt was built to, so its rig,
moves, colliders and rules stay as they are; `conv-<joint>-<tier>` in the Ironworks bank (ironworks_gear.py writes it). Without the bank the code sculpt draws (cloud builds).

Coordinates: V(x, y, z) takes the game's joint frame (x across, y up, z forward; the code sculpt's numbers) into the kit's design frame (X, Y forward, Z up), so gearkit's bake and its dirt read "up" as they should.
"""
import math

import bmesh
from mathutils import Matrix, Vector, noise

from gearkit import Piece, box, catmull, cyl, decal, finish, ribbon, sphere, torus, tube

FRAME = Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
S = 1.1                                  # NativeIronworksBossModel.VesselScale
GIRDER_X, GIRDER_H = 2.46, .27
HIP_X, HIP_F, HIP_R = 2.98, 1.8, -2.45
COXA, FEMUR, TIBIA = .42, 2.35, 3.6
JAW_HINGE, JAW_R = -.3, .47
LEVER = (0, .55, .36)
LAMP_AT = (-GIRDER_X, .72, 2.09)
VALVE_AT = (-.78, 1.02, .3)
PROFILE_R = (.95, 1.12, 1.30, 1.52, 1.68, 1.76, 1.78, 1.76, 1.70, 1.52, 1.28, 1.10, 1.02)
PROFILE_Y = (-1.98, -1.90, -1.72, -1.36, -.92, -.42, 0, .42, .86, 1.36, 1.80, 2.12, 2.40)


def V(x, y=0.0, z=0.0):
    if not isinstance(x, (int, float)):
        x, y, z = x
    return Vector((x, z, y))


def finishes():
    finish('c-iron', 0x24211E, 'metal')
    finish('c-iron-dark', 0x161412, 'metal')
    finish('c-edge', 0x4F4B46, 'metal')
    finish('c-steel', 0x5A5751, 'metal')
    finish('c-chrome', 0x8A877F, 'metal')
    finish('c-bolt', 0x4A4640, 'metal', outline=False)
    finish('c-yellow', 0xC9A23A, 'paint', under=0x4F4B46)
    finish('c-yellow-dark', 0x8A6E26, 'paint', under=0x3A3632)
    finish('c-stripe', 0x1A1714, 'paint', under=0x4F4B46)
    finish('c-green', 0x2F4038, 'paint', under=0x5A5751)
    finish('c-lining', 0x5A3A2A, 'cloth', dirt=.6)
    finish('c-scale', 0x4A2E1E, 'metal')
    finish('c-soot', 0x110F0D, 'rubber', outline=False)
    finish('c-paint', 0xD8D4C8, 'sticker', outline=False)
    finish('c-rubber', 0x151515, 'rubber')
    finish('c-glass', 0x0E1114, 'plastic')
    finish('c-brass', 0x8A6A2A, 'metal')
    finish('c-knob', 0x7A1E18, 'paint', under=0x3A1410)
    finish('c-grate', 0x2A2724, 'metal')
    # slag
    finish('c-slag', 0x110E0F, 'plastic', dirt=.3)
    finish('c-slag-glass', 0x2B272E, 'plastic', dirt=.2)
    finish('c-slag-side', 0x1D191B, 'plastic', dirt=.3)
    finish('c-glint', 0x3D3A44, 'plastic', dirt=.1, outline=False)
    # the suit
    finish('c-silver', 0x8C8F93, 'metal', dirt=.7)
    finish('c-silver-dark', 0x505256, 'metal', dirt=.8)
    finish('c-quilt', 0x6A6D71, 'cloth', dirt=.6)
    finish('c-glove', 0x3A2414, 'cloth')
    finish('c-hat', 0xDCD6C4, 'paint', under=0x8F866E)
    finish('c-belt', 0x2A2622, 'cloth')
    finish('c-buckle', 0x8A7A4A, 'metal')
    # glows: the emission's own colours (Core WeaponPaint.Glow): the runtime scales them by the joint's glow
    finish('c-metal', 0xFFF1D8, 'glow')
    finish('c-molten', 0xFFB04A, 'glow')
    finish('c-molten-dim', 0xA8642A, 'glow')
    finish('c-molten-low', 0x5A3216, 'glow')
    finish('c-tuyere', 0xFFF1D8, 'glow')
    finish('c-seam', 0xFFF1D8, 'glow')
    finish('c-crack', 0x8A3014, 'glow')
    finish('c-tong-hot', 0xB04A1C, 'glow')
    finish('c-visor', 0x5CE8FF, 'glow')
    finish('c-gauge', 0x5CE8FF, 'glow')
    finish('c-lamp', 0xE4EEFF, 'glow')


# ---------------------------------------------------------------------------------------------------------------------------
def span(pc, x0, x1, y0, y1, z0, z1, fin):
    return box(pc, V((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), (abs(x1 - x0), abs(z1 - z0), abs(y1 - y0)), fin)


def bx(pc, x, y, z, sx, sy, sz, fin, rot=None):
    return box(pc, V(x, y, z), (sx, sz, sy), fin, rot=rot)


def rod(pc, a, b, r, fin, segs=12, r1=None):
    return cyl(pc, V(a), V(b), r, fin, segs=segs, r1=r1)


def bolts(pc, pts, axis, r=.03, h=.022, fin='c-bolt', segs=6):
    a = V(axis).normalized()
    for p in pts:
        q = V(p)
        cyl(pc, q - a * .004, q + a * h, r, fin, segs=segs, r1=r * .78)


def shell_r(y):
    if y <= PROFILE_Y[0]:
        return PROFILE_R[0]
    for i in range(1, len(PROFILE_Y)):
        if y <= PROFILE_Y[i]:
            t = (y - PROFILE_Y[i - 1]) / (PROFILE_Y[i] - PROFILE_Y[i - 1])
            return PROFILE_R[i - 1] + (PROFILE_R[i] - PROFILE_R[i - 1]) * t
    return PROFILE_R[-1]


def nose(y):
    t = max(0.0, min(1.0, (y - 1.3) / 1.2))
    return .24 * t * t * (3 - 2 * t)


def on_shell(phi, y, lift=0.0):
    """The shell's frame (Core OnShell, at the vessel's scale): its point and its round, up and out axes, all in design axes."""
    s, c, r = math.sin(phi), math.cos(phi), shell_r(y)
    k = (shell_r(y + .04) - shell_r(y - .04)) / .08
    n = Vector((s, -k, c)).normalized()
    t = Vector((k * s, 1, k * c)).normalized()
    x = t.cross(n)
    p = Vector((s * r, y, c * r + nose(y))) + n * lift
    return V(p * S), V(x), V(t), V(n)


def shell_at(phi, y, lift, lx, ly, lz=0.0):
    p, x, t, n = on_shell(phi, y, lift)
    return p + x * lx * S + t * ly * S + n * lz * S


def lathe(pc, rs, ys, fin, segs=48, nosed=True, scale=S, cap_low=False, cap_high=False):
    """A body of revolution about the joint's y (rs, ys unscaled, the nose's lean when `nosed`), faced outward; `fin` a name, or a per-ring list."""
    rings = []
    for r, y in zip(rs, ys):
        off = nose(y) if nosed else 0.0
        rings.append([pc.bm.verts.new(V(math.sin(2 * math.pi * i / segs) * r * scale, y * scale, (math.cos(2 * math.pi * i / segs) * r + off) * scale)) for i in range(segs)])
    faces = []
    for k in range(len(rings) - 1):
        f = fin if isinstance(fin, str) else fin[k]
        ring_faces = [pc.bm.faces.new((rings[k][i], rings[k][(i + 1) % segs], rings[k + 1][(i + 1) % segs], rings[k + 1][i])) for i in range(segs)]
        for q in ring_faces:
            q.material_index = pc.slot(f)
            q.tag = True
        faces += ring_faces
    if cap_low:
        q = pc.bm.faces.new(rings[0])
        q.material_index = pc.slot(fin if isinstance(fin, str) else fin[0])
        q.tag = True
        faces.append(q)
    if cap_high:
        q = pc.bm.faces.new(rings[-1])
        q.material_index = pc.slot(fin if isinstance(fin, str) else fin[-1])
        q.tag = True
        faces.append(q)
    return faces


def out_from_axis(faces, ref_y=None):
    """Wind a lathe's faces away from its axis (a band of ys may be concave: the rim's inner lining faces in by giving `ref_y`)."""
    for f in faces:
        f.normal_update()
        c = f.calc_center_median()
        if f.normal.dot(Vector((c.x, c.y - (.0 if ref_y is None else ref_y), 0))) < 0:
            f.normal_flip()


def smooth_profile(steps=3):
    pts = [Vector((r, y, 0)) for r, y in zip(PROFILE_R, PROFILE_Y)]
    c = catmull(pts, steps)
    return [p.x for p in c], [p.y for p in c]


# ===========================================================================================================================
# The yoke: riveted girders, trunnion bearings, the tilt drive, hip brackets, the rear arch
# ===========================================================================================================================
def yoke():
    out = []
    pc = Piece('girders')
    gh, gw = .5, GIRDER_H
    for s in (-1, 1):
        x = s * GIRDER_X
        span(pc, x - .08, x + .08, -gh + .06, gh - .06, -2.75, 2.05, 'c-iron')
        span(pc, x - gw, x + gw, gh - .1, gh, -2.75, 2.05, 'c-iron')
        span(pc, x - gw, x + gw, -gh, -gh + .1, -2.75, 2.05, 'c-iron')
        # the top flange's worn yellow walkway
        span(pc, x - gw + .01, x + gw - .01, gh, gh + .018, -2.65, 1.95, 'c-yellow')
        z = -2.5
        while z < 1.95:
            for sd in (-1, 1):
                a, b = x + sd * .08, x + sd * (gw - .01)
                span(pc, min(a, b), max(a, b), -gh + .1, gh - .1, z - .028, z + .028, 'c-edge')
            z += .55
        # end plate with hazard stripes
        span(pc, x - gw - .012, x + gw + .012, -gh - .012, gh + .012, 2.05, 2.1, 'c-iron-dark')
        for i in range(5):
            bx(pc, x, -.38 + i * .19, 2.108, gw * 2, .1, .014, 'c-yellow' if i % 2 == 0 else 'c-stripe', rot=Matrix.Rotation(.6 * s, 3, 'Y'))
        # the trunnion bearing and its pillow cap
        span(pc, x - .34, x + .34, -.7, .5, -.58, .58, 'c-iron-dark')
        bx(pc, x, .62, 0, .7, .36, 1.0, 'c-iron')
        rod(pc, (x - .36, .5, 0), (x + .36, .5, 0), .32, 'c-iron', 24)
        # hip brackets out to the coxae, gusseted
        for hz in (HIP_F, HIP_R):
            span(pc, min(x + s * gw, s * HIP_X), max(x + s * gw, s * HIP_X), -.62, .4, hz - .46, hz + .46, 'c-iron')
            span(pc, min(x + s * gw, s * (HIP_X - .3)), max(x + s * gw, s * (HIP_X - .3)), .4, .44, hz - .4, hz + .4, 'c-edge')
            span(pc, min(x + s * gw, s * (HIP_X - .3)), max(x + s * gw, s * (HIP_X - .3)), -.66, -.62, hz - .4, hz + .4, 'c-edge')
        for cz in (.9, -1.4):
            span(pc, x - .05, x + .05, -.68, -.5, cz - .11, cz + .11, 'c-steel')
    # the rear arch and its beam
    for s in (-1, 1):
        a, t = Vector((s * GIRDER_X, .45, -2.6)), Vector((s * 1.62, 1.95, -3.15))
        d = (t - a).normalized()
        c = (a + t) / 2
        l = (t - a).length + .3
        rot = Vector((0, 1, 0)).rotation_difference(V(d)).to_matrix()
        box(pc, V(c), (.56, l, .56), 'c-iron', rot=rot)
        box(pc, V(c) + rot @ Vector((0, 0, .285)), (.42, l * .8, .02), 'c-yellow', rot=rot)
    span(pc, -1.95, 1.95, 1.72, 2.14, -3.5, -3.0, 'c-iron')
    span(pc, -1.9, 1.9, 2.14, 2.158, -3.45, -3.05, 'c-yellow')
    span(pc, -.5, .5, .8, 1.1, -3.45, -3.15, 'c-iron-dark')
    out.append(pc.bevel(.016, 2, 40).make())
    pc = Piece('yoke-rivets')
    rv = []
    for s in (-1, 1):
        x = s * GIRDER_X
        z = -2.65
        while z < 2.0:
            for ox in (-.2, .2):
                rv.append((x + ox, gh + .012, z))
            z += .3
    bolts(pc, rv, (0, 1, 0), .022, .014)
    rv = []
    for s in (-1, 1):
        x = s * GIRDER_X
        for bz in (-.42, .42):
            for bx_ in (-.24, .24):
                rv.append((x + bx_, .8, bz))
    bolts(pc, rv, (0, 1, 0), .05, .035, fin='c-steel')
    out.append(pc.make())
    # pipes, braces, the steam main on its brackets, the tilt drive
    pc = Piece('yoke-pipes')
    for s in (-1, 1):
        x = s * GIRDER_X
        rod(pc, (x + s * (gw + .12), .18, -2.6), (x + s * (gw + .12), .18, 1.4), .08, 'c-iron-dark', 12)
        z = -2.2
        while z < 1.4:
            lo, hi = sorted((x + s * gw, x + s * (gw + .2)))
            span(pc, lo, hi, .12, .24, z - .04, z + .04, 'c-edge')
            torus(pc, V(x + s * (gw + .12), .18, z), V(0, 0, 1), .09, .012, 'c-steel', segs=12, rsegs=4)
            z += .9
        rod(pc, (s * 1.7, 1.75, -3.25), (0, .95, -3.3), .09, 'c-iron', 10)
    pin = Vector((3.02, -1.24, -.7))
    rod(pc, pin - Vector((.1, 0, 0)), pin + Vector((.17, 0, 0)), .29, 'c-green', 28)
    for i in range(16):
        t = i * math.pi / 8
        q = pin + Vector((.0, math.sin(t) * .3, math.cos(t) * .3))
        bx(pc, q.x + .03, q.y, q.z, .2, .05, .05, 'c-green', rot=Matrix.Rotation(-t, 3, 'X'))
    span(pc, 2.45, 3.25, -1.75, -1.12, -1.5, -.82, 'c-green')
    span(pc, 2.2, 2.65, -1.12, -.5, -1.35, -.95, 'c-iron')
    for i in range(9):
        span(pc, 2.5, 3.2, -1.76, -1.72, -1.46 + i * .074, -1.435 + i * .074, 'c-edge')
    # the motor's conduit up to the girder
    tube(pc, catmull([V(2.5, -1.2, -1.0), V(2.42, -.95, -.9), V(2.42, -.6, -.7), V(2.44, -.5, -.5)], 4), .035, 'c-rubber', segs=6)
    out.append(pc.bevel(.008, 1, 40).make())
    return out


# ===========================================================================================================================
# The vessel: riveted plate courses, hoops, the trunnion ring and pins, the bull gear, the blast pipe, the nameplate
# ===========================================================================================================================
GLYPHS = ("101101101101010", "111100110100111", "011100010001110", "011100010001110", "111100110100111", "100100100100111", "000000000000000", "011100100100011")


def vessel():
    out = []
    pc = Piece('shell')
    rs, ys = smooth_profile(3)
    n = len(rs)
    # the rim, over the lip and down the lining
    rs += [1.09, 1.10, .95, .80, .76]
    ys += [2.52, 2.66, 2.75, 2.70, 2.5]
    fins = ['c-iron'] * (n - 4) + ['c-edge'] * 4 + ['c-edge', 'c-lining', 'c-lining', 'c-lining']
    faces = lathe(pc, rs, ys, fins, segs=64)
    for f in faces:
        f.normal_update()
        c = f.calc_center_median()
        y = c.z / S
        ax = Vector((0, nose(y) * S, c.z))
        o = c - ax
        if y > 2.72:
            want = Vector((0, 0, 1))
        else:
            want = Vector((o.x, o.y, 0))
            if y > 2.5 and math.hypot(o.x, o.y) / S < .95:
                want = -want
        if f.normal.dot(want) < 0:
            f.normal_flip()
    bottom = lathe(pc, (.95, .92, .90, .86, .70), (-1.98, -2.04, -2.2, -2.3, -2.34), 'c-iron-dark', segs=48, nosed=False, cap_low=False)
    out_from_axis(bottom)
    out.append(pc.make(recalc=False))
    # hoops, proud of the shell, and the heavy trunnion ring
    pc = Piece('hoops')
    for hy in (-1.5, -.95, .9, 1.42, 2.05):
        hr = shell_r(hy)
        f = lathe(pc, (hr + .004, hr + .055, hr + .06, hr + .06, hr + .055, hr + .004), (hy - .1, hy - .082, hy - .06, hy + .06, hy + .082, hy + .1), 'c-edge', segs=56)
        for q in f:
            q.normal_update()
            c = q.calc_center_median()
            y = c.z / S
            if q.normal.dot(Vector((c.x, c.y - nose(y) * S, 0))) < 0:
                q.normal_flip()
    f = lathe(pc, (1.79, 1.97, 1.99, 1.99, 1.97, 1.79), (-.38, -.34, -.3, .3, .34, .38), 'c-iron', segs=56, nosed=False)
    out_from_axis(f)
    out.append(pc.bevel(.006, 1, 45).make(recalc=False))
    pc = Piece('vessel-rivets')
    rv = []
    for i in range(24):
        a = i * math.pi / 12
        for ry in (-.2, .2):
            rv.append((math.sin(a) * 2.0 * S, ry * S, math.cos(a) * 2.0 * S))
    for p in rv:
        d = Vector((p[0], 0, p[2])).normalized()
        bolts(pc, [p], d, .05, .03, fin='c-steel')
    for hy in (-1.5, -.95, .9, 1.42):
        hr = shell_r(hy) + .06
        for i in range(20):
            a = i * math.pi / 10
            p = (math.sin(a) * hr * S, hy * S, (math.cos(a) * hr + nose(hy)) * S)
            bolts(pc, [p], (math.sin(a), 0, math.cos(a)), .028, .018)
    out.append(pc.make())
    # plate seams: vertical butt straps between the hoops, their rivets; the lit edges read the curve
    pc = Piece('plate-seams')
    for i in range(8):
        phi = i * math.pi / 4 + math.pi / 8
        for y0, y1 in ((-1.4, -1.05), (-.85, -.42), (.42, .8), (1.0, 1.32)):
            ym = (y0 + y1) / 2
            c, x, t, nn = on_shell(phi, ym, .012)
            rot = Matrix((x, t, nn)).transposed()
            box(pc, c, (.09 * S, (y1 - y0) * S, .022), 'c-edge', rot=rot)
            for k in range(5):
                ly = (y0 - ym) + (y1 - y0) * (k + .5) / 5
                for lx in (-.028, .028):
                    q = c + x * lx * S + t * ly * S + nn * .01
                    cyl(pc, q, q + nn * .014, .011, 'c-bolt', segs=5, r1=.008)
    out.append(pc.bevel(.003, 1, 50).make())
    # the trunnion pins, the bull gear (a real toothed rim, its web and spokes), the blast pipe
    pc = Piece('gear')
    rod(pc, (1.9 * S, 0, 0), (2.98 * S, 0, 0), .3 * S, 'c-steel', 24)
    rod(pc, (-1.9 * S, 0, 0), (-2.62 * S, 0, 0), .3 * S, 'c-steel', 24)
    gx, gw, gr = 2.68 * S, .17 * S, .95 * S
    teeth = 40
    pts = []
    for i in range(teeth):
        a = i * 2 * math.pi / teeth
        w = math.pi / teeth
        for ang, r in ((a - w * .42, gr + .002), (a - w * .22, gr + .125), (a + w * .22, gr + .125), (a + w * .42, gr + .002)):
            pts.append((math.sin(ang) * r, math.cos(ang) * r))
    inner = [(math.sin(i * 2 * math.pi / 40) * (gr - .13), math.cos(i * 2 * math.pi / 40) * (gr - .13)) for i in range(40)]
    a0 = [pc.bm.verts.new(V(gx, y, z)) for y, z in pts]
    a1 = [pc.bm.verts.new(V(gx + gw, y, z)) for y, z in pts]
    b0 = [pc.bm.verts.new(V(gx, y, z)) for y, z in inner]
    b1 = [pc.bm.verts.new(V(gx + gw, y, z)) for y, z in inner]
    fs = []
    m = len(pts)
    for i in range(m):
        fs.append(pc.bm.faces.new((a0[i], a0[(i + 1) % m], a1[(i + 1) % m], a1[i])))
    for i in range(40):
        fs.append(pc.bm.faces.new((b0[i], b0[(i + 1) % 40], b1[(i + 1) % 40], b1[i])))
        j0, j1 = i * 4, (i + 1) * 4 % m
        fs.append(pc.bm.faces.new((a0[j0], a0[j0 + 1], a0[j0 + 2], a0[j0 + 3], a0[j1], b0[(i + 1) % 40], b0[i])))
        fs.append(pc.bm.faces.new((a1[j0], a1[j0 + 1], a1[j0 + 2], a1[j0 + 3], a1[j1], b1[(i + 1) % 40], b1[i])))
    pc.tag(fs, 'c-green')
    for i in range(6):
        a = i * math.pi / 3
        rod(pc, (gx + gw / 2, 0, 0), (gx + gw / 2, math.sin(a) * (gr - .1), math.cos(a) * (gr - .1)), .07, 'c-green', 8)
    rod(pc, (gx - .02, 0, 0), (gx + gw + .05, 0, 0), .36, 'c-green', 24)
    bolts(pc, [(gx + gw + .05, math.sin(i * math.pi / 3) * .26, math.cos(i * math.pi / 3) * .26) for i in range(6)], (1, 0, 0), .035, .03, fin='c-steel')
    out.append(pc.make())
    pc = Piece('blast-pipe')
    path = [(-2.6, 0, 0), (-2.86, 0, 0), (-2.92, -.3, 0), (-2.7, -.85, 0), (-2.2, -1.45, .05), (-1.62, -1.92, .05), (-1.1, -2.18, 0), (-.9, -2.2, 0)]
    pts_ = catmull([V(p[0] * S, p[1] * S, p[2] * S) for p in path], 4)
    tube(pc, pts_, .19 * S, 'c-iron', segs=16)
    for k in (1, 3, 5):
        p, a, b = V(path[k][0] * S, path[k][1] * S, path[k][2] * S), V(path[k - 1][0] * S, path[k - 1][1] * S, path[k - 1][2] * S), V(path[k + 1][0] * S, path[k + 1][1] * S, path[k + 1][2] * S)
        d = (b - a).normalized()
        cyl(pc, p - d * .05, p + d * .05, .28, 'c-edge', segs=18)
        for i in range(8):
            t = i * math.pi / 4
            q = Vector((1, 0, 0)).cross(d)
            if q.length < .1:
                q = Vector((0, 1, 0)).cross(d)
            q.normalize()
            w = d.cross(q)
            o = p + (q * math.cos(t) + w * math.sin(t)) * .235
            cyl(pc, o - d * .07, o + d * .07, .022, 'c-steel', segs=5)
    # the wind box drum's bolts
    bolts(pc, [(math.sin(i * math.pi / 8) * .93 * S, -2.04 * S, math.cos(i * math.pi / 8) * .93 * S) for i in range(16)], (0, -1, 0), .035, .03, fin='c-steel')
    out.append(pc.make())
    # the nameplate, its letters stencilled cream
    pc = Piece('nameplate')
    c, x, t, nn = on_shell(0, -1.06, .022)
    rot = Matrix((x, t, nn)).transposed()
    box(pc, c, (1.46 * S, .46 * S, .03), 'c-iron-dark', rot=rot)
    for sx in (-.66, .66):
        for sy in (-.17, .17):
            q = c + x * sx * S + t * sy * S + nn * .012
            cyl(pc, q, q + nn * .02, .02, 'c-bolt', segs=6, r1=.015)
    out.append(pc.bevel(.008, 1, 50).make())
    pc = Piece('stencil')
    px = .044 * S
    total = (len(GLYPHS) * 4 - 1) * px

    def stencil(a, b):
        # across the plate: a, b from -.5 to .5; the boss's +x is the viewer's left, so the letters run toward -x
        col = (.5 - a) * (len(GLYPHS) * 4 - 1)
        row = (.5 - b) * 5
        g = int(col // 4)
        cc = int(col - g * 4)
        rr = int(row)
        if g < 0 or g >= len(GLYPHS) or cc > 2 or rr < 0 or rr > 4:
            return None
        return 'c-paint' if GLYPHS[g][rr * 3 + cc] == '1' else None
    decal(pc, c + nn * .0165, x, t, total, px * 5 * 1.25, px / 4, stencil)
    out.append(pc.make(recalc=False))
    return out


def vessel_heat():
    out = []
    pc = Piece('heat')
    f = lathe(pc, (.86, .81, .775), (2.72, 2.62, 2.5), 'c-molten', segs=48)
    for q in f:
        q.normal_update()
        c = q.calc_center_median()
        if q.normal.dot(Vector((c.x, c.y - nose(c.z / S) * S, 0))) > 0:
            q.normal_flip()
    f = lathe(pc, (1.05, .99, .88), (2.675, 2.765, 2.738), 'c-molten-dim', segs=48)
    for q in f:
        q.normal_update()
        if q.normal.z < 0:
            q.normal_flip()
    for hy in (-1.5, -.95, .9, 1.42):
        hr = shell_r(hy - .11) + .012
        f = lathe(pc, (hr, hr), (hy - .135, hy - .1), 'c-molten-low', segs=48)
        for q in f:
            q.normal_update()
            c = q.calc_center_median()
            if q.normal.dot(Vector((c.x, c.y - nose(c.z / S) * S, 0))) < 0:
                q.normal_flip()
    # The spill: a tongue over the front lip, thinning down the nose; two trickles beside it (the runtime's molten strip runs over its centre).
    for phi, length, w0 in ((-.4, .55, .12), (.33, .78, .14)):
        pts, nrm, wid = [], [], []
        for i in range(10):
            u = i / 9
            y = 2.7 - length * u
            p, x, t, nn = on_shell(phi + .05 * math.sin(i * 1.9 + phi * 5), y, .03)
            pts.append(p)
            nrm.append(nn)
            wid.append(w0 * (1 - .6 * u) * S)
        ribbon(pc, pts, nrm, w0, .03, 'c-molten', widths=wid)
        p, x, t, nn = on_shell(phi, 2.7 - length - .05, .05)
        sphere(pc, p, (w0 * .32, w0 * .32, w0 * .36), 'c-molten', u=8, v=5)
    out.append(pc.make(recalc=False))
    return out


def metal():
    """The bath's surface (its joint's frame at the surface's centre): a white-hot heart, a darker skin round its edge, slow bubbles."""
    r = .76 * S
    out = []
    pc = Piece('bath')
    n = 32
    rings = [(.001, 0, 'c-metal'), (r * .4, .006, 'c-metal'), (r * .62, .004, 'c-metal'), (r * .86, 0, 'c-molten'), (r, -.02, 'c-molten')]
    vs = [[pc.bm.verts.new(V(math.sin(i * 2 * math.pi / n) * rr, y + .006 * noise.noise(Vector((math.sin(i * 2 * math.pi / n) * rr * 3, math.cos(i * 2 * math.pi / n) * rr * 3, 1.3))), math.cos(i * 2 * math.pi / n) * rr)) for i in range(n)] for rr, y, _ in rings]
    for k in range(len(rings) - 1):
        fs = [pc.bm.faces.new((vs[k][i], vs[k + 1][i], vs[k + 1][(i + 1) % n], vs[k][(i + 1) % n])) for i in range(n)]
        pc.tag(fs, rings[k][2])
        for f in fs:
            f.normal_update()
            if f.normal.z < 0:
                f.normal_flip()
    for i in range(9):
        a = i * 2.4
        rr = r * (.2 + .06 * i)
        sphere(pc, V(math.sin(a) * rr, .014, math.cos(a) * rr), (.05 + .012 * (i % 3), .05 + .012 * (i % 3), .018), 'c-metal', u=10, v=5)
    out.append(pc.make(recalc=False))
    return out


def wind_box():
    pc = Piece('tuyere')
    n = 28
    f = lathe(pc, (.62, .001), (-2.35, -2.35), 'c-tuyere', segs=n, nosed=False)
    for q in f:
        q.normal_update()
        if q.normal.z > 0:
            q.normal_flip()
    f = lathe(pc, (.93, .93), (-2.06, -2.01), 'c-tuyere', segs=n, nosed=False)
    out_from_axis(f)
    out = [pc.make(recalc=False)]
    pc = Piece('tuyere-bars')
    for i in range(-2, 3):
        span(pc, -.6 * S, .6 * S, -2.39 * S, -2.36 * S, (i * .24 - .025) * S, (i * .24 + .025) * S, 'c-iron-dark')
        span(pc, (i * .24 - .025) * S, (i * .24 + .025) * S, -2.4 * S, -2.37 * S, -.6 * S, .6 * S, 'c-iron-dark')
    # a ring of tuyere nozzles round the grate
    for i in range(12):
        a = i * math.pi / 6
        p = Vector((math.sin(a) * .75 * S, -2.33 * S, math.cos(a) * .75 * S))
        rod(pc, p, p + Vector((0, -.09, 0)), .055, 'c-steel', 8, r1=.04)
    out.append(pc.make())
    return out


def seams():
    pc = Piece('seams')
    for k in range(7):
        phi = k * math.pi * 2 / 7 + .3
        y = 2.3
        pts = []
        for i in range(10):
            pts.append(on_shell(phi, y, .014)[0])
            y -= .4
            phi += (HASH(k, i, 31) - .5) * .32
        radii = [.03 * (1 - .05 * i) for i in range(len(pts))]
        tube(pc, pts, .03, 'c-seam', segs=4, radii=radii)
        for i in (2, 5, 8):
            a = pts[i]
            p, x, t, nn = on_shell(phi, 2.3 - i * .4, .014)
            side = x * (.14 if i % 2 else -.14) * S
            tube(pc, [a, a + side * .5 + t * -.08, a + side + t * -.16], .012, 'c-seam', segs=3)
    return [pc.make()]


# ---------------- the crust: Core Plates()' layout, re-cut as glassy fractured slabs, a joint per chunk (the rig flies and tumbles each about its centre) ----------------
_MIX = 0xFFFFFFFF


def _mix(x):
    x &= _MIX
    x ^= x >> 16
    x = (x * 0x7feb352d) & _MIX
    x ^= x >> 15
    x = (x * 0x846ca68b) & _MIX
    x ^= x >> 16
    return x


def KIT_HASH(a, b=0, c=0):
    return _mix((_mix((_mix((a + 0x9E3779B9) & _MIX) ^ ((b * 0x85EBCA6B) & _MIX)) & _MIX) + ((c * 0xC2B2AE35) & _MIX)) & _MIX) / 4294967296.0


def HASH(a, b, c):
    """Core NativeIronworksBossLook.Hash."""
    return KIT_HASH(a + 0x71, b, c)


ROWS = (-.95, -.5, .58, 1.0, 1.42, 1.82)


def plates_table():
    out = []
    for zone in range(4):
        centre = (0, -math.pi / 2, math.pi / 2, math.pi)[zone]
        side = zone in (1, 2)
        for row in range(len(ROWS)):
            for col in range(4):
                y = ROWS[row] + (HASH(zone, row, col) - .5) * .08
                if side and abs(y) < .6:
                    continue
                if zone == 0 and y < -.45:
                    continue
                if y > 1.9:
                    continue
                off = 0 if row % 2 == 0 else .5
                phi = centre + ((col + .5 + off * .6) / 4 - .5) * 1.5 + (HASH(zone, col, row + 7) - .5) * .08
                w = .82 + HASH(zone, row, col + 3) * .3 - .12 * max(0, y - 1.2)
                h = .6 + HASH(zone, row + 5, col) * .18
                t = .16 + HASH(zone, row, col + 9) * .05
                lowest = row == 0 or (zone == 0 and row == 2) or (side and row == 2)
                out.append(dict(zone=zone, row=row, col=col, chunk=0 if row >= 4 else 1 if row >= 2 else 2, phi=phi, y=y, w=w, h=h, t=t, turn=(HASH(zone, row + 2, col + 4) - .5) * .3, lowest=lowest))
    return out


def crust(zone, chunk):
    pc = Piece('slabs')
    seams_ = Piece('slab-seams')
    for pl in plates_table():
        if pl['zone'] != zone or pl['chunk'] != chunk:
            continue
        k = pl['zone'] * 97 + pl['row'] * 13 + pl['col']
        p0, x, t_, n_ = on_shell(pl['phi'], pl['y'], 0)
        turn, tilt = pl['turn'], -.12
        cz, sz = math.cos(turn), math.sin(turn)
        xx = x * cz + t_ * sz
        yy = -x * sz + t_ * cz
        cx, sx = math.cos(tilt), math.sin(tilt)
        yy2 = yy * cx + n_ * sx
        nn2 = -yy * sx + n_ * cx
        w, h, th = pl['w'] * .56 * S, pl['h'] * .5 * S, pl['t'] * S
        corners = 9
        outline = []
        for i in range(corners):
            a = math.pi / 2 + (i + .5 * (HASH(k, i, 20) - .5)) * 2 * math.pi / corners
            j = .72 + .5 * HASH(k, i, 22)
            rx, ry = w * j, h * (.8 + .4 * HASH(k, i, 23))
            yv = math.sin(a) * ry
            if yv < 0:
                yv *= 1.15
            outline.append((math.cos(a) * rx, yv))

        def ring(inset, z):
            cu = sum(o[0] for o in outline) / corners
            cv = sum(o[1] for o in outline) / corners
            return [pc.bm.verts.new(p0 + xx * (o[0] + (cu - o[0]) * inset) + yy2 * (o[1] + (cv - o[1]) * inset) + nn2 * (z + .016 * noise.noise(Vector((o[0] * 7, o[1] * 7, k * .3))))) for o in outline]
        rings = [ring(.06, -.02), ring(0, th * .45), ring(0, th * .8), ring(.22, th)]
        # a facetted top: a raised, off-centre point (a conchoidal break)
        apex = pc.bm.verts.new(p0 + xx * (HASH(k, 2, 35) - .5) * w * .5 + yy2 * (HASH(k, 3, 36) - .5) * h * .5 + nn2 * (th + .03 + .03 * HASH(k, 1, 34)))
        for q in range(3):
            fin = 'c-slag-side' if q < 2 else 'c-slag-glass'
            for i in range(corners):
                f = pc.bm.faces.new((rings[q][i], rings[q][(i + 1) % corners], rings[q + 1][(i + 1) % corners], rings[q + 1][i]))
                f.material_index = pc.slot(fin)
                f.tag = True
                f.normal_update()
                if f.normal.dot(nn2 * .3 + (f.calc_center_median() - p0).normalized()) < 0:
                    f.normal_flip()
        for i in range(corners):
            f = pc.bm.faces.new((rings[3][i], rings[3][(i + 1) % corners], apex))
            f.material_index = pc.slot('c-slag' if (i * 3 + k) % 4 else 'c-glint')
            f.tag = True
            f.normal_update()
            if f.normal.dot(nn2) < 0:
                f.normal_flip()
        # the dim seam under its lower edge
        c0 = p0 + yy2 * (-h * .92) + nn2 * (th * .3)
        box(seams_, c0, (w * 1.5, .035 * S, th * .55), 'c-crack', rot=Matrix((xx, yy2, nn2)).transposed())
        # glassy drips off the lowest row
        if pl['lowest'] and HASH(k, 5, 38) < .75:
            for d in range(2):
                dx = (d - .5) * w * .8 + (HASH(k, d, 39) - .5) * .1
                ln = .14 + HASH(k, d, 40) * .16
                a = p0 + xx * dx + yy2 * (-h * .95) + nn2 * (th * .45)
                b = a + yy2 * (-ln) + nn2 * (-.01)
                cyl(pc, a, b, .05, 'c-slag', segs=6, r1=.016)
                sphere(pc, b, (.024, .024, .03), 'c-slag-glass', u=6, v=4)
    pc.sharp = 24
    return [pc.make(recalc=False), seams_.make()]


# ===========================================================================================================================
# The pulpit, its levers, the valve wheel; the lamps
# ===========================================================================================================================
def pulpit():
    out = []
    pc = Piece('deck')
    span(pc, -1.12, 1.12, -.18, -.1, -.64, .64, 'c-iron')
    span(pc, -1.1, 1.1, -.1, -.04, -.62, .62, 'c-grate')
    for i in range(10):
        span(pc, -1.08 + i * .24, -1.04 + i * .24, -.04, .0, -.6, .6, 'c-iron-dark')
    for i in range(6):
        span(pc, -1.08, 1.08, -.04, -.006, -.55 + i * .22, -.53 + i * .22, 'c-iron-dark')
    # heat shield and its slit, quadrant, gauge box
    span(pc, -.92, .92, 0, 1.08, .54, .62, 'c-iron')
    span(pc, -.5, .5, .82, .94, .622, .632, 'c-glass')
    span(pc, -.4, .4, 0, .55, .24, .48, 'c-iron-dark')
    span(pc, -.36, .36, .52, .58, .26, .46, 'c-edge')
    span(pc, .58, .98, .8, 1.38, .3, .5, 'c-iron-dark')
    out.append(pc.bevel(.012, 1, 45).make())
    pc = Piece('rails')
    for x in (-1.06, 1.06):
        for z in (-.58, 0, .56):
            rod(pc, (x, 0, z), (x, 1.05, z), .032, 'c-yellow', 8)
        for h in (.55, 1.04):
            rod(pc, (x, h, -.6), (x, h, .6), .028, 'c-yellow', 8)
    for x in (-.36, .36):
        rod(pc, (x, 0, -.58), (x, 1.05, -.58), .032, 'c-yellow', 8)
    for h in (.55, 1.04):
        rod(pc, (-1.08, h, -.58), (1.08, h, -.58), .028, 'c-yellow', 8)
    rod(pc, (-.78, 0, .3), VALVE_AT, .04, 'c-iron', 8)
    rod(pc, (.9, 0, -.5), (.9, 1.7, -.5), .05, 'c-iron', 8)
    rod(pc, (.9, 1.7, -.5), (.9, 2.05, -.5), .09, 'c-brass', 12)
    rod(pc, (.9, 2.05, -.5), (.9, 2.1, -.5), .115, 'c-brass', 12, r1=.06)
    tube(pc, catmull([V(.2, .55, .36), V(.5, .3, .42), V(.7, .1, .45)], 3), .02, 'c-rubber', segs=5)
    out.append(pc.make())
    pc = Piece('shield-rivets')
    bolts(pc, [(-.84 + i * .28, 1.05, .62) for i in range(7)], (0, 0, 1), .022, .012)
    out.append(pc.make())
    pc = Piece('glows')
    span(pc, -.48, .48, .83, .93, .632, .636, 'c-molten-low')
    for i in range(3):
        gy = .92 + i * .16
        rod(pc, (.78, gy, .286), (.78, gy, .3), .06, 'c-gauge', 12)
    out.append(pc.make())
    return out


def lever():
    pc = Piece('levers')
    lp = Vector(LEVER)
    for x in (-.2, .2):
        rod(pc, lp + Vector((x, 0, 0)), lp + Vector((x, .88, -.08)), .03, 'c-steel', 8)
        bx(pc, x, lp.y + .9, lp.z - .08, .1, .1, .1, 'c-knob')
    rod(pc, lp + Vector((-.3, 0, 0)), lp + Vector((.3, 0, 0)), .06, 'c-edge', 10)
    return [pc.bevel(.008, 1, 45).make()]


def valve():
    pc = Piece('wheel')
    torus(pc, V(0, 0, 0), V(0, 0, 1), .26, .03, 'c-green', segs=24, rsegs=8)
    for i in range(3):
        a = i * math.pi / 3
        rod(pc, (math.cos(a) * .26, math.sin(a) * .26, 0), (-math.cos(a) * .26, -math.sin(a) * .26, 0), .022, 'c-green', 6)
    rod(pc, (0, 0, -.03), (0, 0, .04), .05, 'c-brass', 10)
    return [pc.make()]


def lamp():
    out = []
    pc = Piece('lamp-body')
    # facing +z: a square cast housing back to the girder, the bezel, a hood
    span(pc, -.23, .23, -.26, .2, -.5, -.01, 'c-iron-dark')
    rod(pc, (0, 0, -.03), (0, 0, .05), .2, 'c-edge', 20)
    bx(pc, 0, .18, 0, .42, .04, .2, 'c-iron')
    for i in (-1, 0, 1):
        bx(pc, 0, i * .07, .085, .32, .018, .018, 'c-iron-dark')
    for sx in (-1, 1):
        rod(pc, (sx * .23, -.2, -.25), (sx * .3, -.26, -.25), .03, 'c-steel', 6)
    out.append(pc.bevel(.008, 1, 45).make())
    pc = Piece('lamp-lens')
    sphere(pc, V(0, 0, .04), (.155, .04, .155), 'c-lamp', u=18, v=8)
    out.append(pc.make())
    return out


# ===========================================================================================================================
# The foreman: dead at his levers in an aluminised suit, hooded under a white hard hat
# ===========================================================================================================================
def foreman():
    pc = Piece('legs')
    for x in (-.12, .12):
        bx(pc, x, .08, .05, .16, .16, .32, 'c-rubber')
        tube(pc, catmull([V(x, .14, 0), V(x, .5, .006), V(x, .86, 0)], 4), .09, 'c-silver', segs=10, radii=[.078, .08, .083, .086, .088, .09, .092, .094, .095])
        for i in range(3):
            torus(pc, V(x, .24 + i * .18, 0), V(0, 1, 0), .09, .007, 'c-silver-dark', segs=10, rsegs=3)
    # the coat's skirt, panelled and quilted
    rings = []
    for y, rx, rz in ((.5, .3, .2), (.68, .28, .19), (.92, .27, .18)):
        rings.append([pc.bm.verts.new(V(math.sin(i * 2 * math.pi / 20) * rx, y, math.cos(i * 2 * math.pi / 20) * rz)) for i in range(20)])
    fs = []
    for k in range(2):
        for i in range(20):
            fs.append(pc.bm.faces.new((rings[k][i], rings[k][(i + 1) % 20], rings[k + 1][(i + 1) % 20], rings[k + 1][i])))
    pc.tag(fs, 'c-silver')
    for f in fs:
        f.normal_update()
        c = f.calc_center_median()
        if f.normal.dot(Vector((c.x, c.y, 0))) < 0:
            f.normal_flip()
    for y in (.56, .68, .8):
        torus(pc, V(0, y, 0), V(0, 1, 0), .285, .006, 'c-quilt', segs=20, rsegs=3)
    return [pc.make(recalc=False)]


def chest():
    pc = Piece('chest')
    # the coat from the waist up (the chest joint at the waist): a rounded trunk, quilted, belt, cape over the shoulders
    rings = []
    prof = ((-.02, .27, .18), (.14, .27, .185), (.34, .3, .19), (.46, .33, .2), (.56, .36, .21), (.63, .3, .19), (.68, .14, .13))
    for y, rx, rz in prof:
        rings.append([pc.bm.verts.new(V(math.sin(i * 2 * math.pi / 24) * rx, y, math.cos(i * 2 * math.pi / 24) * rz)) for i in range(24)])
    fs = []
    for k in range(len(prof) - 1):
        for i in range(24):
            fs.append(pc.bm.faces.new((rings[k][i], rings[k][(i + 1) % 24], rings[k + 1][(i + 1) % 24], rings[k + 1][i])))
    pc.tag(fs, 'c-silver')
    for f in fs:
        f.normal_update()
        c = f.calc_center_median()
        if f.normal.dot(Vector((c.x, c.y, c.z - .34 * 0))) < 0 and abs(f.normal.z) < .9:
            f.normal_flip()
    for y, r in ((.1, .28), (.24, .29), (.38, .31)):
        torus(pc, V(0, y, 0), V(0, 1, 0), r, .007, 'c-quilt', segs=24, rsegs=3)
    torus(pc, V(0, 0, 0), V(0, 1, 0), .28, .03, 'c-belt', segs=24, rsegs=4)
    bx(pc, 0, 0, .185, .1, .07, .03, 'c-buckle')
    # the respirator canister, its hose to the hood's port
    rod(pc, (.12, .34, .2), (.12, .5, .2), .06, 'c-iron-dark', 10)
    tube(pc, catmull([V(.12, .5, .22), V(.1, .6, .23), V(.06, .66, .2)], 3), .02, 'c-rubber', segs=6)
    out = [pc.make(recalc=False)]
    pc = Piece('chest-lamp')
    bx(pc, .12, .46, .262, .035, .02, .01, 'c-gauge')
    out.append(pc.make())
    return out


def head():
    pc = Piece('hood')
    bx(pc, 0, .16, 0, .37, .38, .37, 'c-silver')
    bx(pc, 0, -.02, -.01, .44, .07, .42, 'c-silver-dark')
    bx(pc, 0, .18, .186, .3, .13, .016, 'c-glass')
    out = [pc.bevel(.03, 2, 40).make()]
    pc = Piece('hat')
    # the white foreman's hard hat over the hood: a real dome, ribbed, its brim's peak
    r = bmesh.ops.create_uvsphere(pc.bm, u_segments=28, v_segments=14, radius=1.0)
    for v in r['verts']:
        if v.co.z < 0:
            v.co.z *= .1
        v.co = Vector((v.co.x * .215, v.co.y * .235 + .01, v.co.z * .17 + .36))
    pc.tag(pc.bm.faces, 'c-hat')
    out.append(pc.make())
    pc = Piece('hat-brim')
    a = ellipse_(0, .02, .225, .245, .345, 32)
    b = [Vector((p.x * 1.22, .02 + (p.y - .02) * (1.26 if p.y > .02 else 1.1), .335)) for p in a]
    va, vb = [pc.bm.verts.new(p) for p in a], [pc.bm.verts.new(p) for p in b]
    fs = [pc.bm.faces.new((va[i], vb[i], vb[(i + 1) % 32], va[(i + 1) % 32])) for i in range(32)]
    pc.tag(fs, 'c-hat')
    for f in fs:
        f.normal_update()
        if f.normal.z < 0:
            f.normal_flip()
    out.append(pc.solidify(.012, -1.0).make(recalc=False))
    pc = Piece('hat-rib')
    tube(pc, [Vector((0, .01 + .24 * math.cos(p), .36 + .175 * math.sin(p))) for p in [math.radians(a2) for a2 in range(20, 161, 14)]], .018, 'c-hat', segs=6)
    torus(pc, V(0, .4, .01), V(0, 1, 0), .217, .014, 'c-belt', segs=28, rsegs=4)
    out.append(pc.make())
    return out


def ellipse_(cx, cy, rx, ry, z, n):
    return [Vector((cx + rx * math.sin(2 * math.pi * i / n), cy + ry * math.cos(2 * math.pi * i / n), z)) for i in range(n)]


def visor():
    pc = Piece('visor')
    bx(pc, 0, 0, 0, .27, .052, .008, 'c-visor')
    bx(pc, 0, -.035, -.002, .23, .012, .006, 'c-visor')
    return [pc.make()]


def arm_upper(side):
    pc = Piece('arm-u')
    rod(pc, (0, 0, -.02), (0, 0, .4), .085, 'c-silver', 12, r1=.08)
    bx(pc, side * .04, .04, .04, .2, .15, .22, 'c-silver')
    torus(pc, V(0, 0, .25), V(0, 0, 1), .086, .007, 'c-silver-dark', segs=12, rsegs=3)
    return [pc.bevel(.008, 1, 40).make()]


def arm_fore():
    pc = Piece('arm-f')
    rod(pc, (0, 0, 0), (0, 0, .3), .077, 'c-silver', 12, r1=.072)
    rod(pc, (0, 0, .24), (0, 0, .34), .095, 'c-glove', 12, r1=.08)
    bx(pc, 0, -.01, .38, .11, .1, .12, 'c-glove')
    return [pc.bevel(.008, 1, 40).make()]


# ===========================================================================================================================
# The legs: slewing coxa, box-girder femur and its ram, a long tibia with shin guards, tongs on the front, crane bogies on the rear
# ===========================================================================================================================
def box_loft(pc, sections, fin_side, fin_edge):
    """A tapered box girder along +z: sections (w, h, chamfer, z)."""
    rings = []
    for w, h, c, z in sections:
        hw, hh = w / 2, h / 2
        pts = [(-hw + c, -hh), (hw - c, -hh), (hw, -hh + c), (hw, hh - c), (hw - c, hh), (-hw + c, hh), (-hw, hh - c), (-hw, -hh + c)]
        rings.append([pc.bm.verts.new(V(x, y, z)) for x, y in pts])
    fs = []
    for k in range(len(rings) - 1):
        for i in range(8):
            f = pc.bm.faces.new((rings[k][i], rings[k][(i + 1) % 8], rings[k + 1][(i + 1) % 8], rings[k + 1][i]))
            f.material_index = pc.slot(fin_edge if i % 2 == 1 else fin_side)
            f.tag = True
            fs.append(f)
    for r, want in ((rings[0], -1), (rings[-1], 1)):
        f = pc.bm.faces.new(r)
        f.material_index = pc.slot(fin_side)
        f.tag = True
        f.normal_update()
        if f.normal.y * want < 0:
            f.normal_flip()
    for f in fs:
        f.normal_update()
        c = f.calc_center_median()
        if f.normal.dot(Vector((c.x, 0, c.z))) < 0:
            f.normal_flip()


def coxa():
    pc = Piece('coxa')
    rod(pc, (0, -.62, 0), (0, .55, 0), .48, 'c-iron', 28)
    rod(pc, (0, .34, 0), (0, .44, 0), .56, 'c-edge', 28)
    rod(pc, (0, -.66, 0), (0, -.56, 0), .5, 'c-iron-dark', 28)
    span(pc, -.3, .3, -.38, .32, .12, COXA + .05, 'c-iron')
    for s in (-1, 1):
        span(pc, s * .36 - .06, s * .36 + .06, -.46, .4, COXA - .32, COXA + .24, 'c-iron-dark')
    bx(pc, 0, .56, 0, .62, .03, .62, 'c-yellow', rot=Matrix.Rotation(math.pi / 4, 3, 'Z'))
    bx(pc, 0, .575, 0, .5, .01, .12, 'c-stripe', rot=Matrix.Rotation(math.pi / 4, 3, 'Z'))
    out = [pc.bevel(.012, 1, 45).make()]
    pc = Piece('coxa-bolts')
    pts = [(math.sin(i * math.pi / 10) * .54, .44, math.cos(i * math.pi / 10) * .54) for i in range(20)]
    bolts(pc, pts, (0, 1, 0), .03, .024, fin='c-steel')
    out.append(pc.make())
    return out


def femur():
    L = FEMUR
    pc = Piece('femur')
    box_loft(pc, ((.74, .8, .12, 0), (.72, .78, .12, L * .28), (.58, .62, .1, L * .9), (.54, .56, .09, L)), 'c-iron', 'c-edge')
    out = [pc.make(recalc=False)]
    pc = Piece('femur-parts')
    for i in range(4):
        z = .3 + i * .5
        hw, hh = .37 - .035 * i, .4 - .036 * i
        span(pc, -hw - .016, hw + .016, -hh - .016, hh + .016, z - .035, z + .035, 'c-edge')
    # lightening panels on the sides
    for i in range(3):
        z = .55 + i * .62
        hh = .24 - .02 * i
        for s in (-1, 1):
            hw = .37 - .058 * z
            span(pc, s * hw - .006, s * hw + .006, -hh, hh, z - .2, z + .2, 'c-iron-dark')
    span(pc, -.11, .11, .38, .62, .02, .22, 'c-iron-dark')
    span(pc, -.1, .1, .3, .56, L * .88, L * .99, 'c-iron-dark')
    # the knee hub on its hinge, striped caps
    rod(pc, (-.42, 0, L), (.42, 0, L), .5, 'c-iron', 28)
    for s in (-1, 1):
        rod(pc, (s * .42, 0, L), (s * .46, 0, L), .38, 'c-yellow', 24)
        rod(pc, (s * .46, 0, L), (s * .52, 0, L), .14, 'c-iron-dark', 12)
        for i in range(3):
            bx(pc, s * .462, 0, L + (i - 1) * .2, .012, .66, .08, 'c-stripe', rot=Matrix.Rotation(.55, 3, 'X'))
    out.append(pc.bevel(.008, 1, 45).make())
    pc = Piece('femur-ram')
    rod(pc, (0, .58, .12), (0, .55, L * .6), .15, 'c-iron-dark', 16)
    rod(pc, (0, .55, L * .6), (0, .5, L * .94), .075, 'c-chrome', 12)
    rod(pc, (0, .55, L * .58), (0, .55, L * .61), .17, 'c-edge', 16)
    for s in (-1, 1):
        tube(pc, catmull([V(s * .2, .42, .15), V(s * .3, .5, L * .35), V(s * .26, .36, L * .7)], 5), .04, 'c-rubber', segs=6)
    out.append(pc.make())
    pc = Piece('femur-rivets')
    pts = []
    z = .12
    while z < L * .9:
        hh = .4 - .07 * z / L
        for x in (-.27, .27):
            pts.append((x * (1 - .2 * z / L), hh + .002, z))
        z += .15
    bolts(pc, pts, (0, 1, 0), .022, .015)
    out.append(pc.make())
    return out


def tibia():
    L = TIBIA
    pc = Piece('tibia')
    box_loft(pc, ((.62, .68, .1, .3), (.6, .66, .1, L * .35), (.46, .5, .08, L * .9), (.44, .48, .08, L)), 'c-iron', 'c-edge')
    out = [pc.make(recalc=False)]
    pc = Piece('tibia-parts')
    for i in range(3):
        z = .8 + i * .72
        h = .2 - .02 * i
        for s in (-1, 1):
            span(pc, s * (.3 - .02 * i) - .008, s * (.3 - .02 * i) + .008, -h, h, z - .24, z + .24, 'c-iron-dark')
    # the shin guard: three overlapping plates down the outer face
    for i in range(3):
        z0, z1, w = .45 + i * .82, .45 + i * .82 + .95, .6 - .06 * i
        bx(pc, 0, .36 - .03 * i, (z0 + z1) / 2, w, .06, z1 - z0, 'c-yellow' if i == 0 else 'c-iron', rot=Matrix.Rotation(-.04, 3, 'X'))
    span(pc, -.09, .09, -.46, -.3, .2, .38, 'c-iron-dark')
    span(pc, -.08, .08, -.32, -.22, L * .64, L * .7, 'c-iron-dark')
    span(pc, -.24, .24, -.26, .26, L * .72, L * .93, 'c-scale')
    for i in range(4):
        span(pc, -.245, .245, -.265, .265, L * .74 + i * .05, L * .74 + i * .05 + .012, 'c-soot')
    rod(pc, (-.3, 0, 0), (.3, 0, 0), .27, 'c-iron', 20)
    out.append(pc.bevel(.008, 1, 45).make())
    pc = Piece('tibia-ram')
    rod(pc, (0, -.4, .3), (0, -.36, L * .42), .12, 'c-iron-dark', 14)
    rod(pc, (0, -.36, L * .42), (0, -.3, L * .66), .06, 'c-chrome', 10)
    rod(pc, (0, -.36, L * .4), (0, -.36, L * .43), .135, 'c-edge', 14)
    out.append(pc.make())
    pc = Piece('tibia-rivets')
    pts = []
    for i in range(3):
        z0, w = .45 + i * .82, .6 - .06 * i
        for k in range(3):
            pts.append(((k - 1) * w * .38, .395 - .03 * i, z0 + .2))
            pts.append(((k - 1) * w * .38, .395 - .03 * i, z0 + .75))
    bolts(pc, pts, (0, 1, 0), .03, .02)
    out.append(pc.make())
    return out


def foot_front():
    pc = Piece('ankle')
    bx(pc, 0, -.04, 0, .6, .48, .66, 'c-iron')
    bx(pc, 0, .21, 0, .5, .03, .56, 'c-yellow')
    rod(pc, (-.4, JAW_HINGE, 0), (.4, JAW_HINGE, 0), .15, 'c-steel', 16)
    for s in (-1, 1):
        rod(pc, (s * .4, JAW_HINGE, 0), (s * .44, JAW_HINGE, 0), .19, 'c-edge', 16)
    return [pc.bevel(.016, 2, 45).make()]


def jaw(which):
    s = 1 if which == 0 else -1
    hinge = Vector((0, JAW_HINGE, 0))
    centre = hinge + Vector((0, -JAW_R, 0))
    pc = Piece('jaw')
    # a forged curved bar in two cheek plates, teeth along its inside
    pts, hot = [], []
    for i in range(13):
        a = math.pi * (.04 + .92 * i / 12)
        pts.append(centre + Vector((0, math.cos(a) * JAW_R, s * math.sin(a) * JAW_R)))
    for x in (-.09, .09):
        path = [V(x, p.y, p.z) for p in pts]
        ribbon(pc, path, [(path[i] - V(0, centre.y, centre.z) - Vector((x, 0, 0))).normalized() for i in range(len(path))], .08, .02, 'c-iron')
    tube(pc, [V(0, p.y, p.z) for p in pts[:10]], .085, 'c-iron', segs=8, radii=[.1 - .004 * i for i in range(10)])
    out = [pc.make()]
    pc = Piece('jaw-hot')
    tip_path = [V(0, p.y, p.z) for p in pts[9:]]
    tube(pc, tip_path, .06, 'c-tong-hot', segs=8, radii=[.07, .062, .054, .03])
    tip = centre + Vector((0, -JAW_R - .05, s * .03))
    bx(pc, 0, tip.y, tip.z, .2, .1, .14, 'c-tong-hot')
    out.append(pc.make())
    pc = Piece('jaw-teeth')
    for i in range(2, 10):
        p = pts[i]
        d = (centre - p).normalized()
        q = p + d * .13
        cyl(pc, V(0, q.y, q.z), V(0, q.y + d.y * .08, q.z + d.z * .08), .05, 'c-edge', segs=6, r1=.012)
    bx(pc, 0, JAW_HINGE, 0, .3, .36, .36, 'c-iron')
    out.append(pc.make())
    return out


def foot_rear():
    pc = Piece('bogie')
    bx(pc, 0, -.04, 0, .56, .46, .6, 'c-iron')
    bx(pc, 0, .2, 0, .44, .03, .48, 'c-yellow')
    span(pc, -.38, .38, -.6, -.24, -.82, .82, 'c-iron')
    span(pc, -.39, .39, -.28, -.24, -.78, .78, 'c-yellow')
    for i in range(6):
        bx(pc, -.395, -.42, -.6 + i * .24, .01, .3, .07, 'c-stripe', rot=Matrix.Rotation(.6, 3, 'X'))
    for z in (-.88, .88):
        bx(pc, 0, -.44, z, .42, .18, .1, 'c-iron-dark')
    out = [pc.bevel(.012, 2, 45).make()]
    pc = Piece('wheels')
    for z in (-.5, .5):
        rod(pc, (-.24, -.5, z), (.24, -.5, z), .34, 'c-steel', 24)
        rod(pc, (-.24, -.5, z), (-.18, -.5, z), .38, 'c-iron-dark', 24)
        rod(pc, (.18, -.5, z), (.24, -.5, z), .38, 'c-iron-dark', 24)
        rod(pc, (-.28, -.5, z), (.28, -.5, z), .08, 'c-edge', 12)
        for i in range(6):
            a = i * math.pi / 3
            bolts(pc, [(.24, -.5 + math.sin(a) * .22, z + math.cos(a) * .22)], (1, 0, 0), .025, .018)
    out.append(pc.bevel(.006, 1, 50).make())
    pc = Piece('bogie-soot')
    span(pc, -.36, .36, -.84, -.82, -.72, .72, 'c-soot')
    out.append(pc.make())
    return out


def chain_link():
    pc = Piece('link')
    ring = [Vector((math.sin(a) * .036, 0, math.cos(a) * .07)) for a in [i * 2 * math.pi / 12 for i in range(12)]]
    tube(pc, [V(p.x, p.y, p.z) for p in ring], .014, 'c-iron-dark', segs=6, closed=True)
    return [pc.make()]


def chain_hook():
    pc = Piece('c-hook')
    torus(pc, V(0, -.03, 0), V(0, 0, 1), .05, .018, 'c-steel', segs=14, rsegs=6)
    rod(pc, (0, -.08, 0), (0, -.46, 0), .045, 'c-iron', 12)
    rod(pc, (0, -.22, 0), (0, -.26, 0), .06, 'c-edge', 12)
    path = []
    for i in range(10):
        a = math.pi * (1.15 - i / 9 * 1.3)
        path.append(V(math.cos(a) * .17, -.62 - math.sin(a) * .17, 0))
    tube(pc, [V(-.17 * .0, -.46, 0)] + path, .05, 'c-iron', segs=10, radii=[.05] + [max(.008, .052 - .0045 * i) for i in range(10)])
    return [pc.make()]


def chain_shackle():
    pc = Piece('shackle')
    path = [V(-.07, 0, 0), V(-.075, -.1, 0), V(-.05, -.2, 0), V(0, -.235, 0), V(.05, -.2, 0), V(.075, -.1, 0), V(.07, 0, 0)]
    tube(pc, catmull(path, 3), .022, 'c-steel', segs=8)
    rod(pc, (-.1, 0, 0), (.1, 0, 0), .02, 'c-iron-dark', 8)
    return [pc.make()]


# ===========================================================================================================================
# Grime in the vertex colours (no triangles): as Core NativeIronworksBossLook.Grime: soot from the mouth, heat scale low on the shell, dirt up the legs
# ===========================================================================================================================
def _smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def _lerp(a, b, t):
    return a + (b - a) * max(0.0, min(1.0, t))


def grime(gm, kind):
    glow = set(gm.submeshes[1]) if len(gm.submeshes) > 1 else set()
    for i, (p, c) in enumerate(zip(gm.positions, gm.colors)):
        if i in glow:
            continue
        x, y, z = p
        k, warm = 1.0, 0.0
        if kind == 'vessel':
            yy = y / S
            k *= _lerp(.72, 1.0, _smooth((yy + 2.2) / 1.4))
            if yy > 1.6:
                k *= _lerp(1.0, .7, _smooth((z + .2) / 1.2) * _smooth((yy - 1.6) / .9))
            if abs(yy) < .5:
                k *= .9
            warm = .12 * _smooth((-yy - .6) / 1.2)
            streak = abs(math.sin(math.atan2(x, z) * 7.3 + 1.1))
            if yy > .2:
                k *= _lerp(.86, 1.0, streak)
        elif kind == 'yoke':
            k *= _lerp(.8, 1.0, _smooth((y + .6) / 1.1))
            if z < -2.4:
                k *= .9
        elif kind == 'tibia':
            u = max(0.0, min(1.0, z / TIBIA))
            k *= _lerp(1.0, .62, _smooth((u - .35) / .65))
            warm = .1 * _smooth((u - .6) / .4)
        elif kind == 'femur':
            k *= .84 if y < 0 else 1.0
        elif kind == 'foot':
            k *= _lerp(.6, .9, _smooth((y + .6) / .7))
        elif kind == 'jaw':
            k *= _lerp(.7, 1.0, _smooth((y + .9) / .7))
        if k != 1.0 or warm > 0:
            gm.colors[i] = (min(1, c[0] * k * (1 + warm)), min(1, c[1] * k * (1 + warm * .35)), min(1, c[2] * k * (1 - warm * .3)), c[3])


CRUST_NAMES = ('crust-front', 'crust-left', 'crust-right', 'crust-back')


def parts():
    """(joint name, its build, its grime kind): the names are Core NativeIronworksBossLook.Names."""
    out = [('yoke', yoke, 'yoke'), ('vessel', vessel, 'vessel'), ('vessel-heat', vessel_heat, None), ('metal', metal, None), ('wind-box', wind_box, None), ('seams', seams, None),
           ('pulpit', pulpit, None), ('lever', lever, None), ('valve', valve, None), ('lamp-l', lamp, None), ('lamp-r', lamp, None),
           ('foreman', foreman, None), ('foreman-chest', chest, None), ('foreman-head', head, None), ('foreman-visor', visor, None),
           ('arm-upper-l', lambda: arm_upper(-1), None), ('arm-upper-r', lambda: arm_upper(1), None), ('arm-fore-l', arm_fore, None), ('arm-fore-r', arm_fore, None)]
    for g, name in enumerate(CRUST_NAMES):
        out.append((name, lambda g=g: crust(g, 0), None))
        out.append((name + '-mid', lambda g=g: crust(g, 1), None))
        out.append((name + '-low', lambda g=g: crust(g, 2), None))
    for leg in ('fl', 'fr', 'rl', 'rr'):
        out += [(leg + '-coxa', coxa, None), (leg + '-femur', femur, 'femur'), (leg + '-tibia', tibia, 'tibia'), (leg + '-foot', foot_front if leg[0] == 'f' else foot_rear, 'foot')]
    for leg in ('fl', 'fr'):
        out += [(leg + '-jaw-front', lambda: jaw(0), 'jaw'), (leg + '-jaw-back', lambda: jaw(1), 'jaw')]
    return out


CHAIN = (('conv-link', chain_link), ('conv-hook', chain_hook), ('conv-shackle', chain_shackle))
# the tiers' Decimate ratios
TIERS = (1.0, .55, .2)
