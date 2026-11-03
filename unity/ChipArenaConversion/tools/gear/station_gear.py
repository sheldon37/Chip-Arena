"""The Central Station kinds' gear (docs/EXPEDITION-ENEMY-REWORK.md section 2; Core NativeZombieLooks Kit), modelled in Blender
and written as the gear bank Resources/ChipArenaPackGear (gearkit.py: the bake, the bone frames, the bank).

usage: blender --background --factory-startup --python tools/gear/station_gear.py -- --out ASSETS/LocalAssets [--preview DIR] [--only id,...]

Every prop rides one bone of the player's model (the bake's skeleton) and was fitted to the zombies wearing it: their meshes in
that bone's frame, measured from the local tool's zdump (BoZo, docs/ZOMBIE-MODELS.md "Gear"). The fits are numbers here, no
mesh of the bake is read:
- the head (every variant wears the same head piece; a variant's head scale scales its gear): the skull spans x +-.095 (its
  widest at y .06 to .14), z -.083 to .134 (the face +z), its crown at y .215; the brows at y .07 to .093, the ears at y .049;
- spine_05 (its frame: x down the spine, y back, z right): the jacket's back stands .13 to .155 behind it (the brute's), .12
  to .136 (the women's) from 3 cm above to 21 cm below it; the shoulders' and chest's outline at 9.5 cm either side is the
  contour each harness follows (CONTOUR);
- lowerarm_r (x along the forearm to the wrist): the sleeve's radius is about .065 from 7 to 15 cm, .055 at the wrist.
"""
import argparse
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gearkit as gk  # noqa: E402
from gearkit import Piece, box, catmull, cyl, decal, finish, ribbon, sphere, torus, tube  # noqa: E402

BANK = 'ChipArenaPackGear'
# Design frame (Blender: X right, Y forward, Z up) to each bone's own frame (Unity's axes).
HEAD = Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))        # x right, y up, z forward
CHEST = Matrix(((0, 0, -1, 0), (0, -1, 0, 0), (1, 0, 0, 0), (0, 0, 0, 1)))     # spine_05: x down, y back, z right
FOREARM = Matrix.Identity(4)                                                   # lowerarm_r: designed in its own axes
FOREARM_L = Matrix.Diagonal((-1, 1, 1, 1))                                     # lowerarm_l: its x runs back to the elbow
PROP = HEAD                                                                    # a carried prop: x across, y up, z its front


# ===========================================================================================================================
# Finishes (the station's palette: Core NativeStationModels)
# ===========================================================================================================================
def finishes():
    finish('hat-yellow', 0xF2C200, 'paint', under=0xE9DFB4)
    finish('hat-white', 0xD8D6CC, 'paint', under=0xB4B0A4)
    finish('hat-band', 0x1C1C1E, 'cloth')
    finish('harness', 0x2B2C2E, 'plastic')
    finish('lamp-body', 0x2E3236, 'plastic')
    finish('chrome', 0x9AA3A8, 'metal')
    finish('steel', 0x6E777C, 'metal')
    finish('lens', 0xFFF0C8, 'glow')
    finish('crack', 0x1A1A18, 'sticker', outline=False)
    finish('cable', 0x16161A, 'rubber')
    finish('cable-red', 0xB82A20, 'rubber')
    finish('switch', 0x8C1E18, 'rubber', outline=False)
    finish('screw', 0x8E969A, 'metal', outline=False)
    finish('decal-yellow', 0xF2C200, 'sticker', outline=False)
    finish('decal-black', 0x16161A, 'sticker', outline=False)
    finish('decal-navy', 0x1F3A68, 'sticker', outline=False)
    finish('decal-white', 0xE8E8E2, 'sticker', outline=False)
    finish('decal-red', 0xC0281E, 'sticker', outline=False)
    finish('reflect', 0xD8DDE0, 'sticker', outline=False)
    finish('pack', 0x343B41, 'paint', under=0x878D90)
    finish('pack-dark', 0x1E2226, 'paint', under=0x6A7074)
    finish('pad', 0x23261F, 'cloth')
    finish('webbing', 0x2F3326, 'cloth')
    finish('bumper', 0x141416, 'rubber')
    finish('vent', 0x4FC3FF, 'glow')
    finish('led-green', 0x44FF66, 'glow')
    finish('led-amber', 0xFFB020, 'glow')
    finish('led-red', 0xFF3020, 'glow')
    finish('dial', 0xE8E2D0, 'sticker', outline=False)
    finish('brass', 0xC8A040, 'metal')
    finish('copper', 0xC07A3A, 'metal', outline=False)
    finish('clamp-red', 0xC0281E, 'rubber')
    finish('clamp-black', 0x1A1A1C, 'rubber')
    finish('badge', 0xD4AA48, 'metal', outline=False)
    finish('enamel', 0x7A1A1A, 'sticker', outline=False)
    finish('nickel', 0xC5CAD0, 'metal', outline=False)
    finish('nickel-line', 0xC5CAD0, 'metal')
    finish('slot', 0x101012, 'sticker', outline=False)
    finish('tin', 0x3A3D40, 'paint', under=0x8E9294)
    finish('tin-dark', 0x24262A, 'paint', under=0x707478)
    finish('lantern-glass', 0xE0281C, 'glow')
    finish('glass-dark', 0x5A0C08, 'plastic')
    finish('weed', 0x1E2B20, 'cloth', dirt=.5)
    finish('weed-olive', 0x34442A, 'cloth', dirt=.5)
    finish('weed-brown', 0x3A3222, 'cloth', dirt=.5)
    finish('rail-yellow', 0xE8C020, 'paint', under=0x9AA4A8)
    finish('stainless', 0x8A9AA0, 'metal')
    finish('stainless-dark', 0x5A666B, 'metal')
    finish('seal', 0x101214, 'rubber')
    finish('livery', 0xC0302A, 'paint', under=0x8A9AA0)
    finish('cream', 0xE8E2D0, 'paint', under=0x8A9AA0)
    finish('window', 0x6FB3A0, 'glow')
    finish('tube-light', 0xF4FFFA, 'glow')
    finish('sleeve', 0x4A4D55, 'cloth')
    finish('dead-skin', 0x8C9286, 'cloth')
    finish('nail', 0x3A302A, 'cloth', outline=False)
    finish('scratch', 0xB8C4C8, 'sticker', outline=False)
    finish('flesh', 0x9A948A, 'flesh')
    finish('vein', 0x4A2A3A, 'flesh', outline=False)
    finish('bone', 0xD8CFB8, 'paint', under=0x8A7A60)
    finish('suit-torn', 0x4A4D55, 'cloth')


# ===========================================================================================================================
# The hard hat (head): a moulded shell over the skull on its suspension, three ribs, a brim with a front peak and a rain lip,
# an elastic strap carrying the headlamp (its lens cracked), the lamp's cable round to the back, stickers.
# ===========================================================================================================================
C = Vector((0, .025, .105))                  # the shell's centre: the skull's, its widest band
R = Vector((.112, .128, .135))               # inside: 1.5 cm over the skull's sides, 2.5 cm over the crown
TH = .0045
RO = R + Vector((TH, TH, TH))


def cut(y):
    """The shell's lower edge: above the brows in front, over the ears, lower behind."""
    return .095 + .10 * (y - C.y)


def dome(z):
    """The outer shell's radii scale at height z (vertical walls below the centre)."""
    return math.sqrt(max(0.0, 1 - ((z - C.z) / RO.z) ** 2)) if z > C.z else 1.0


def onshell(p, off=0.0):
    """`p` moved onto the outer shell along its ray from the centre (the walls along the level), out by `off`; and the normal."""
    q = Vector(p) - C
    if q.z > 0:
        s = 1 / math.sqrt((q.x / RO.x) ** 2 + (q.y / RO.y) ** 2 + (q.z / RO.z) ** 2)
        at = C + q * s
        n = Vector(((at.x - C.x) / RO.x ** 2, (at.y - C.y) / RO.y ** 2, (at.z - C.z) / RO.z ** 2)).normalized()
    else:
        s = 1 / math.sqrt((q.x / RO.x) ** 2 + (q.y / RO.y) ** 2)
        at = C + Vector((q.x * s, q.y * s, q.z))
        n = Vector(((at.x - C.x) / RO.x ** 2, (at.y - C.y) / RO.y ** 2, 0)).normalized()
    return at + n * off, n


def ring(z, extra, theta):
    """The point at angle theta (0 the front, + to the right) round the outer shell at height z, `extra` out."""
    k = dome(z)
    return Vector((math.sin(theta) * (RO.x * k + extra), C.y + math.cos(theta) * (RO.y * k + extra), z))


def hardhat(colour):
    out = []
    # The shell: an ellipsoid over the skull, its walls dropped straight below the centre, cut along the brim line.
    pc = Piece('hat-shell')
    r = bmesh.ops.create_uvsphere(pc.bm, u_segments=48, v_segments=24, radius=1.0)
    for v in r['verts']:
        x, y, z = v.co
        if z >= 0:
            v.co = C + Vector((x * R.x, y * R.y, z * R.z))
        else:
            h = math.hypot(x, y)
            d = (x / h, y / h) if h > 1e-6 else (0.0, 0.0)
            v.co = C + Vector((d[0] * R.x, d[1] * R.y, z * .06))
    pc.tag(pc.bm.faces, colour)
    geom = list(pc.bm.verts) + list(pc.bm.edges) + list(pc.bm.faces)
    bmesh.ops.bisect_plane(pc.bm, geom=geom, plane_co=Vector((0, C.y, cut(C.y))), plane_no=Vector((0, -.10, 1)).normalized(), clear_inner=True)
    out.append(pc.solidify(TH, 1.0).bevel(.0012, 1, 65).make(recalc=False))

    # The brim: round the shell's foot, 1.2 cm out at the sides and back, a 6 cm peak in front angled down; a rain lip.
    pc = Piece('hat-brim')
    n = 72
    inner, outer, lip = [], [], []
    for i in range(n):
        t = 2 * math.pi * i / n
        d = Vector((math.sin(t), math.cos(t), 0))
        y = C.y + d.y * RO.y
        p = Vector((d.x * RO.x, y, cut(y)))
        nx = Vector((d.x / RO.x, d.y / RO.y, 0)).normalized()
        front = max(0.0, math.cos(t)) ** 2.5
        w = .012 + .05 * front
        droop = .004 + .014 * front
        inner.append(pc.bm.verts.new(p - nx * .003 + Vector((0, 0, -.0006))))
        o = p + nx * w + Vector((0, 0, -droop))
        outer.append(pc.bm.verts.new(o))
        lip.append(o + Vector((0, 0, .0022)) - nx * .0006)
    faces = [pc.bm.faces.new((inner[i], outer[i], outer[(i + 1) % n], inner[(i + 1) % n])) for i in range(n)]
    pc.tag(faces, colour)
    for f in faces:
        f.normal_update()
        if f.normal.z < 0:
            f.normal_flip()
    out.append(pc.solidify(.0042, -1.0).bevel(.0012, 1, 65).make(recalc=False))
    pc = Piece('hat-lip')
    tube(pc, lip, .0026, colour, segs=6, closed=True)
    out.append(pc.make())

    # Three moulded ribs over the crown.
    pc = Piece('hat-ribs')
    for x0, front_z, back_z in ((0.0, .178, .125), (-.046, .14, .13), (.046, .14, .13)):
        k = math.sqrt(1 - (x0 / RO.x) ** 2)
        pts = []
        for i in range(41):
            ph = math.pi * i / 40
            y = C.y + RO.y * k * math.cos(ph)
            z = C.z + RO.z * k * math.sin(ph)
            if (math.cos(ph) > 0 and z < front_z) or (math.cos(ph) <= 0 and z < back_z):
                continue
            pts.append(Vector((x0, y, z)) + (Vector((x0, y, z)) - C).normalized() * .0012)
        m = len(pts)
        radii = [.0055 * min(1.0, (min(i, m - 1 - i) + 1) / 5.0) ** .5 for i in range(m)]
        tube(pc, pts, .0055, colour, segs=6, radii=radii)
    out.append(pc.make())

    # The suspension's headband inside, round the skull.
    pc = Piece('hat-headband')
    ring_lo, ring_hi = [], []
    for i in range(36):
        t = 2 * math.pi * i / 36
        ring_lo.append(pc.bm.verts.new(Vector((math.sin(t) * .101, .025 + math.cos(t) * .116, .094))))
        ring_hi.append(pc.bm.verts.new(Vector((math.sin(t) * .099, .025 + math.cos(t) * .114, .116))))
    faces = [pc.bm.faces.new((ring_lo[i], ring_lo[(i + 1) % 36], ring_hi[(i + 1) % 36], ring_hi[i])) for i in range(36)]
    pc.tag(faces, 'harness')
    for f in faces:
        f.normal_update()
        c = f.calc_center_median() - Vector((0, .025, f.calc_center_median().z))
        if f.normal.dot(c) < 0:
            f.normal_flip()
    out.append(pc.solidify(.003, 1.0).make(recalc=False))

    # The lamp's elastic strap round the shell, its buckle behind.
    pc = Piece('hat-strap')
    lo = [pc.bm.verts.new(ring(.140, .0015, 2 * math.pi * i / 56)) for i in range(56)]
    hi = [pc.bm.verts.new(ring(.160, .0015, 2 * math.pi * i / 56)) for i in range(56)]
    faces = [pc.bm.faces.new((lo[i], lo[(i + 1) % 56], hi[(i + 1) % 56], hi[i])) for i in range(56)]
    pc.tag(faces, 'hat-band')
    for f in faces:
        f.normal_update()
        c = f.calc_center_median() - Vector((0, C.y, f.calc_center_median().z))
        if f.normal.dot(c) < 0:
            f.normal_flip()
    out.append(pc.solidify(.0028, 1.0).make(recalc=False))
    pc = Piece('hat-buckle')
    back = ring(.15, .0045, math.pi)
    for dx, dz, sx, sz in ((0, .0115, .028, .004), (0, -.0115, .028, .004), (-.012, 0, .004, .027), (.012, 0, .004, .027), (0, 0, .003, .022)):
        box(pc, back + Vector((dx, -.001, dz)), (sx, .004, sz), 'chrome')
    out.append(pc.bevel(.0008, 1).make())

    # The headlamp: a mount on the strap, its housing aimed a little down, the bezel, the lens (cracked), a switch, a gland.
    front = ring(.15, .0043, 0.0)
    tilt = math.radians(17)
    plate_rot = Matrix.Rotation(-tilt, 3, 'X')
    pc = Piece('hat-lamp')
    box(pc, front + Vector((0, .003, 0)), (.036, .006, .03), 'lamp-body', rot=plate_rot)
    for sx in (-.014, .014):
        cyl(pc, front + Vector((sx, .006, .009)), front + Vector((sx, .0085, .0095)), .0025, 'screw', segs=8)
    a = Vector((0, math.cos(math.radians(-8)), math.sin(math.radians(-8))))
    back_c = front + Vector((0, .006, 0))
    front_c = back_c + a * .034
    cyl(pc, back_c, front_c, .021, 'lamp-body', segs=24)
    torus(pc, front_c, a, .0185, .0036, 'chrome', segs=28, rsegs=8)
    cyl(pc, front_c - a * .002, front_c + a * .0006, .0168, 'lens', segs=24)
    u = Vector((1, 0, 0))
    v = a.cross(u).normalized() * -1
    lens = front_c + a * .0012
    for path in (((-.012, .006), (-.003, .0), (.004, .008)), ((-.003, .0), (.009, -.011)), ((-.003, 0), (-.006, -.012))):
        tube(pc, [lens + u * px + v * py for px, py in path], .0007, 'crack', segs=4)
    up = a.cross(u).normalized()
    if up.z < 0:
        up = -up
    top = back_c + a * .02 + up * .021
    cyl(pc, top, top + up * .006, .005, 'switch', segs=10)
    gland = back_c + a * .006 - up * .015 + u * .012
    cyl(pc, gland, gland + Vector((.008, -.002, -.004)), .0045, 'cable', segs=8)
    out.append(pc.bevel(.0015, 2, 65).make())

    # The cable: from the gland along the strap's foot round the right to the back, then down off the back of the hat.
    pc = Piece('hat-cable')
    pts = [gland + Vector((.008, -.002, -.004)), ring(.137, .006, math.radians(14))]
    pts += [ring(.137, .006, math.radians(a)) for a in range(30, 181, 15)]
    pts += [Vector((0, -.112, .118)), Vector((.002, -.114, .095)), Vector((.004, -.118, .072))]
    tube(pc, catmull(pts, 3), .0032, 'cable', segs=6)
    out.append(pc.make())

    # Stickers: the high-voltage triangle (left), the station's roundel (right), reflective tape behind.
    pc = Piece('hat-stickers')

    def sticker(theta, z, w, h, pattern):
        p, nrm = onshell(ring(z, 0, theta))
        u = (-nrm).cross(Vector((0, 0, 1))).normalized()
        v = nrm.cross(u).normalized()
        decal(pc, p, u, v, w, h, .003, pattern, project=lambda q: onshell(q, .0007)[0])

    def inside(poly, a, b):
        c = False
        for i in range(len(poly)):
            (x1, y1), (x2, y2) = poly[i], poly[i - 1]
            if (y1 > b) != (y2 > b) and a < (x2 - x1) * (b - y1) / (y2 - y1) + x1:
                c = not c
        return c
    tri = ((0, .46), (-.5, -.42), (.5, -.42))
    tri_in = ((0, .3), (-.36, -.33), (.36, -.33))
    bolt = ((.06, .24), (-.1, -.04), (.02, -.04), (-.07, -.3), (.13, .04), (0, .04), (.1, .24))

    def hazard(a, b):
        if not inside(tri, a, b):
            return None
        if inside(bolt, a, b) or not inside(tri_in, a, b):
            return 'decal-black'
        return 'decal-yellow'

    def roundel(a, b):
        r = math.hypot(a, b)
        if r > .5:
            return None
        if r > .42 or (abs(a) < .3 and (.08 < b < .16 or -.16 < b < -.08)) or (abs(b) < .2 and any(abs(a - x) < .03 for x in (-.2, 0, .2))):
            return 'decal-white'
        return 'decal-navy'
    sticker(-math.pi / 2 - .15, .17, .046, .044, hazard)
    sticker(math.pi / 2 + .15, .17, .042, .042, roundel)
    sticker(math.pi, .128, .085, .018, lambda a, b: 'reflect')
    out.append(pc.make(recalc=False))
    return out


# ===========================================================================================================================
# The battery pack (spine_05): a rugged case on two pads, its corner guards, a vent unit glowing blue through louvres (the weak
# point), a dial and three status lights, the terminals in their boots with their cables into the side, a carry handle,
# hazard labels; its harness over the shoulders to a sternum strap, and two straps from its foot round the ribs.
# ===========================================================================================================================
# The outline (Y, Z) of the shoulders and chest 9.5 cm out from the spine, back to front (the zdump of linesman-male-a and
# linesman-female-a: the jacket's outermost point in each 2 cm band).
CONTOUR = {
    'm': [(-.125, .04), (-.10, .07), (-.08, .094), (-.06, .11), (-.04, .123), (-.02, .13), (0, .12), (.02, .11), (.05, .045), (.068, 0.0),
          (.09, -.032), (.112, -.06), (.122, -.09), (.125, -.12), (.115, -.15)],
    'f': [(-.12, .024), (-.10, .056), (-.08, .088), (-.05, .108), (-.02, .115), (0, .103), (.02, .083), (.04, .052), (.06, .03), (.08, .005),
          (.105, -.025), (.128, -.05), (.14, -.075), (.144, -.1), (.14, -.125), (.13, -.15)],
}
# The pads' face against the back (the brute's back .155 behind spine_05 at most, the women's .136).
PAD = {'m': -.160, 'f': -.140}


def offset_contour(contour, off, x):
    P = catmull([Vector((0, y, z)) for y, z in contour], 4)
    pts, nrm = [], []
    centre = Vector((0, 0, -.05))
    for i, p in enumerate(P):
        t = (P[min(i + 1, len(P) - 1)] - P[max(i - 1, 0)]).normalized()
        n = Vector((0, -t.z, t.y))
        if n.dot(p - centre) < 0:
            n = -n
        pts.append(Vector((x, p.y, p.z)) + n * off)
        nrm.append(n)
    return pts, nrm


def pack(build):
    out = []
    yp = PAD[build]
    yc, yo = yp - .01, yp - .11            # the case's back and outer faces
    zt, zb = .055, -.205                    # its top and foot
    ym, zm = (yc + yo) * .5, (zt + zb) * .5
    pc = Piece('pack-case')
    box(pc, (0, ym, zm), (.27, yc - yo, zt - zb), 'pack')
    box(pc, (0, ym, zt - .036), (.274, yc - yo + .004, .006), 'pack-dark')      # the lid's rim
    out.append(pc.bevel(.011, 2, 65).make())
    pc = Piece('pack-pads')
    for x in (-.07, .07):
        box(pc, (x, yc + .006, zm + .01), (.062, .014, .22), 'pad')
    out.append(pc.bevel(.005, 2, 65).make())
    pc = Piece('pack-guards')
    for sx in (-1, 1):
        box(pc, (sx * .128, yo + .011, zm), (.03, .03, zt - zb + .008), 'bumper')
        box(pc, (sx * .128, yc - .011, zm), (.03, .03, zt - zb + .008), 'bumper')
    out.append(pc.bevel(.006, 1, 65).make())

    # The vent unit: a frame proud of the outer face, the glowing cells behind seven louvres, four screws.
    pc = Piece('pack-vent')
    vz = -.068
    for dx, dz, sx, sz in ((0, .059, .2, .012), (0, -.059, .2, .012), (-.094, 0, .012, .13), (.094, 0, .012, .13)):
        box(pc, (dx, yo - .007, vz + dz), (sx, .014, sz), 'pack-dark')
    for i in range(7):
        box(pc, (0, yo - .008, vz - .045 + i * .015), (.176, .005, .013), 'pack-dark', rot=Matrix.Rotation(math.radians(35), 3, 'X'))
    for dx in (-.094, .094):
        for dz in (-.059, .059):
            cyl(pc, (dx, yo - .014, vz + dz), (dx, yo - .0165, vz + dz), .003, 'screw', segs=8)
    out.append(pc.bevel(.0015, 2, 65).make())
    pc = Piece('pack-cells')
    box(pc, (0, yo - .0015, vz), (.178, .002, .108), 'vent')
    out.append(pc.make())

    # The dial (top left) and the three status lights (top right).
    pc = Piece('pack-dial')
    dc = Vector((-.07, yo - .004, .02))
    torus(pc, dc, (0, -1, 0), .019, .0038, 'chrome', segs=28, rsegs=8)
    cyl(pc, dc + Vector((0, .004, 0)), dc + Vector((0, -.002, 0)), .0175, 'pack-dark', segs=24)

    def dial(a, b):
        r = math.hypot(a, b)
        if r > .5:
            return None
        ang = math.degrees(math.atan2(b, a))
        if .36 < r < .46 and 10 < ang < 60:
            return 'decal-red'
        if .4 < r < .47 and any(abs(ang - t) < 3 for t in range(-30, 211, 30)):
            return 'decal-black'
        return 'dial'
    decal(pc, dc + Vector((0, -.0022, 0)), (1, 0, 0), (0, 0, 1), .033, .033, .0016, dial)
    nd = Vector((math.cos(math.radians(125)), 0, math.sin(math.radians(125))))
    tube(pc, [dc + Vector((0, -.0032, 0)), dc + Vector((0, -.0032, 0)) + nd * .013], .0009, 'decal-black', segs=4)
    cyl(pc, dc + Vector((0, -.0025, 0)), dc + Vector((0, -.0045, 0)), .0022, 'screw', segs=8)
    lc = Vector((.068, yo - .003, .02))
    box(pc, lc, (.054, .006, .016), 'pack-dark')
    out.append(pc.bevel(.0008, 1, 65).make())
    pc = Piece('pack-leds')
    for i, f in enumerate(('led-green', 'led-amber', 'led-red')):
        cyl(pc, lc + Vector((-.016 + i * .016, -.002, 0)), lc + Vector((-.016 + i * .016, -.0055, 0)), .0042, f, segs=12)
    out.append(pc.make())

    # Labels: hazard stripes under the vents, a high-voltage plate on the right side.
    pc = Piece('pack-labels')
    decal(pc, (0, yo - .0006, -.162), (1, 0, 0), (0, 0, 1), .2, .03, .003,
          lambda a, b: 'decal-yellow' if int(math.floor((a * .2 + b * .03) / .012)) % 2 == 0 else 'decal-black')

    def volts(a, b):
        if abs(a) > .46 or abs(b) > .46:
            return 'decal-black'
        bolt = ((.08, .34), (-.14, -.02), (.02, -.02), (-.1, -.36), (.18, .06), (.0, .06), (.14, .34))
        c = False
        for i in range(len(bolt)):
            (x1, y1), (x2, y2) = bolt[i], bolt[i - 1]
            if (y1 > b) != (y2 > b) and a < (x2 - x1) * (b - y1) / (y2 - y1) + x1:
                c = not c
        return 'decal-black' if c else 'decal-yellow'
    decal(pc, (.1356, ym, -.05), (0, 1, 0), (0, 0, 1), .06, .07, .0035, volts)
    out.append(pc.make(recalc=False))

    # The terminals in their boots (red +, black -), their cables arcing into the sides; the carry handle.
    pc = Piece('pack-top')
    for sx, boot, wire in ((1, 'clamp-red', 'cable-red'), (-1, 'clamp-black', 'cable')):
        base = Vector((sx * .098, yo + .022, zt))
        cyl(pc, base, base + Vector((0, 0, .016)), .0105, 'brass', segs=12)
        cyl(pc, base + Vector((0, 0, .008)), base + Vector((0, 0, .034)), .0155, boot, segs=14, r1=.009)
        pts = [base + Vector((0, 0, .03)), base + Vector((sx * .018, -.004, .05)), Vector((sx * .14, yo + .02, zt + .022)),
               Vector((sx * .147, yo + .035, zt - .03)), Vector((sx * .146, yo + .045, -.03)), Vector((sx * .139, yo + .045, -.052))]
        tube(pc, catmull(pts, 3), .0075, wire, segs=8)
        cyl(pc, (sx * .135, yo + .045, -.056), (sx * .148, yo + .045, -.056), .011, 'bumper', segs=12)
    hp = [Vector((-.05, ym, zt - .002)), Vector((-.042, ym, zt + .026)), Vector((0, ym, zt + .034)), Vector((.042, ym, zt + .026)), Vector((.05, ym, zt - .002))]
    tube(pc, catmull(hp, 4), .0068, 'bumper', segs=8)
    for x in (-.05, .05):
        box(pc, (x, ym, zt + .004), (.018, .022, .01), 'steel')
    out.append(pc.bevel(.0012, 1, 65).make())

    # The harness: the shoulder straps over the contour from the case's top, down the chest to the ribs; the sternum strap
    # and its buckle; adjusters; D-rings on the case; the foot straps round to the sides.
    pc = Piece('pack-harness')
    for sx in (-1, 1):
        pts, nrm = offset_contour(CONTOUR[build], .011, sx * .095)
        start = [Vector((sx * .085, yc - .004, zt - .004)), Vector((sx * .09, yc + .02, zt + .002))]
        tail = [Vector((sx * .12, pts[-1].y - .02, pts[-1].z - .022)), Vector((sx * .15, pts[-1].y - .05, pts[-1].z - .036)), Vector((sx * .175, pts[-1].y - .09, pts[-1].z - .04))]
        path = start + pts + tail
        normals = [Vector((0, -.3, 1)), Vector((0, -.5, 1))] + nrm + [Vector((sx * .3, 1, -.2)), Vector((sx * .7, .7, -.1)), Vector((sx, .3, 0))]
        ribbon(pc, path, normals, .045, .005, 'webbing')
        # The adjuster partway down the chest.
        k = min(range(len(pts)), key=lambda i: abs(pts[i].z + .105))
        box(pc, pts[k] + nrm[k] * .007, (.05, .004, .016), 'steel', rot=Vector((0, 0, 1)).rotation_difference(nrm[k]).to_matrix() @ Matrix.Rotation(math.pi / 2, 3, 'X'))
        torus(pc, (sx * .085, yc - .009, zt - .002), (1, 0, 0), .013, .0026, 'steel', segs=16, rsegs=6)
        foot = [Vector((sx * .11, yc - .004, zb + .03)), Vector((sx * .15, yp + .035, zb + .012)), Vector((sx * .178, yp + .1, zb + .008))]
        ribbon(pc, foot, [Vector((sx * .5, -1, 0)), Vector((sx * .8, -.6, 0)), Vector((sx, -.2, 0))], .035, .005, 'webbing')
    z = -.07
    k = min(range(len(pts)), key=lambda i: abs(pts[i].z - z))
    yf = pts[k].y + nrm[k].y * .005
    chest = [Vector((-.095, yf, z)), Vector((-.045, yf + .006, z)), Vector((.045, yf + .006, z)), Vector((.095, yf, z))]
    ribbon(pc, chest, [Vector((0, 1, 0))] * 4, .022, .004, 'webbing')
    box(pc, (0, yf + .011, z), (.044, .008, .03), 'harness')
    box(pc, (0, yf + .0155, z), (.016, .003, .012), 'pack-dark')
    out.append(pc.bevel(.0008, 1, 65).make())
    return out


# ===========================================================================================================================
# The jumper cables coiled round the right forearm (lowerarm_r: x down the forearm), red and black wound together, their
# crocodile clamps bitten onto the coil with the cables running out of their handles.
# ===========================================================================================================================
def clamp(pc, tip, along, up, jaws, sleeve):
    """A crocodile clamp: copper jaws at `tip` biting, its handles along `along` (7 cm), opening toward `up`."""
    a = Vector(along).normalized()
    u = (Vector(up) - a * Vector(up).dot(a)).normalized()
    w = a.cross(u)
    rot = Matrix((a, u, w)).transposed()
    for s in (1, -1):
        # Jaws: tapered plates meeting at the tip, teeth along their inner edges.
        for i in range(5):
            x = .004 + i * .0045
            box(pc, Vector(tip) + a * x + u * s * .0032, (.0042, .0026, .016 - i * .0012), jaws, rot=rot)
        for i in range(4):
            x = .003 + i * .0055
            box(pc, Vector(tip) + a * x + u * s * .0013, (.0016, .0018, .012), jaws, rot=rot @ Matrix.Rotation(math.radians(45), 3, 'Z'))
        # Handles: the steel lever under its insulation, opening a little from the pivot.
        h0 = Vector(tip) + a * .026 + u * s * .006
        h1 = h0 + a * .045 + u * s * .007
        cyl(pc, h0, h1, .0068, sleeve, segs=10)
        box(pc, h1 + a * .004 + u * s * .001, (.008, .006, .015), sleeve, rot=rot)
    cyl(pc, Vector(tip) + a * .025 - w * .009, Vector(tip) + a * .025 + w * .009, .0042, 'steel', segs=10)
    torus(pc, Vector(tip) + a * .025, w, .0065, .0013, 'steel', segs=12, rsegs=5)


def coil():
    out = []
    pc = Piece('coil')
    axis_y, axis_z = -.003, -.007

    def rad(x):
        return .076 if x < .12 else .076 - (x - .12) / .05 * .008
    x0, x1, turns = .07, .165, 3.2
    ends = []
    for phase, fin in ((0.0, 'cable-red'), (math.pi, 'cable')):
        pts = []
        for i in range(int(turns * 18) + 1):
            f = i / (turns * 18)
            x = x0 + (x1 - x0) * f
            t = phase + 2 * math.pi * turns * f
            pts.append(Vector((x, axis_y + rad(x) * math.cos(t), axis_z + rad(x) * math.sin(t))))
        tube(pc, pts, .0072, fin, segs=8)
        ends.append((pts[-1], pts[-2], fin))
    # The clamps lie along the coil's top and bottom, jaws toward the elbow, their cables out of the handles into the coil's ends.
    pk = Piece('coil-clamps')
    for (end, prev, fin), ang, jaws, sleeve in zip(ends, (0.25, math.pi + .25), ('copper', 'copper'), ('clamp-red', 'clamp-black')):
        r = .076 + .013
        out_dir = Vector((0, math.cos(ang), math.sin(ang)))
        tip = Vector((.088, axis_y, axis_z)) + out_dir * r
        clamp(pk, tip, (1, 0, 0), out_dir, jaws, sleeve)
        handle_end = tip + Vector((.078, 0, 0))
        tube(pc, catmull([handle_end, handle_end + Vector((.012, 0, 0)) - out_dir * .004, end + (end - prev).normalized() * .01, end], 4), .0072, fin, segs=10)
    out.append(pc.make())
    out.append(pk.make())
    return out


# ===========================================================================================================================
# The Conductor (head, spine_05): the railway's winged wheel in brass on red enamel on the cap's crown; the nickel whistle on
# its link chain round the neck, down over the lapels.
# ===========================================================================================================================
def badge():
    pc = Piece('badge')
    c = Vector((0, .156, .174))                          # the cap's crown front (the MilitaryHat: z .145 to .15 at y .18 to .2)
    n = Vector((0, 1, .18)).normalized()
    u = Vector((1, 0, 0))
    v = n.cross(u).normalized() * -1
    if v.z < 0:
        v = -v
    rot = Matrix((u, n, v)).transposed()                 # local x across, y out of the cap, z up the badge

    def at(x, y, z):
        return c + u * x + n * y + v * z
    cyl(pc, at(0, -.001, 0), at(0, .0015, 0), .011, 'enamel', segs=20)
    torus(pc, at(0, .0018, 0), n, .0085, .0016, 'badge', segs=20, rsegs=5)
    cyl(pc, at(0, .001, 0), at(0, .0035, 0), .0028, 'badge', segs=10)
    for k in range(6):
        a = k * math.pi / 3
        box(pc, at(math.cos(a) * .0045, .0018, math.sin(a) * .0045), (.0075, .0012, .0013), 'badge', rot=rot @ Matrix.Rotation(a, 3, 'Y'))
    # The wings: four swept feathers a side, longest on top.
    for sx in (-1, 1):
        for k in range(4):
            length = .021 - k * .0035
            ang = math.radians(16 - k * 13)
            base = Vector((sx * .0105, 0, .0032 - k * .0033))
            mid = base + Vector((sx * math.cos(ang) * length * .5, 0, math.sin(ang) * length * .5))
            box(pc, at(mid.x, .0016, mid.z), (length, .0016, .0036 - k * .0003), 'badge', rot=rot @ Matrix.Rotation(-sx * ang, 3, 'Y'))
    return [pc.make()]


def whistle():
    out = []
    pc = Piece('chain')
    for sx in (-1, 1):
        pts = [Vector((sx * x, y, z)) for x, y, z in ((.07, .0, .11), (.062, .045, .075), (.05, .062, .05), (.038, .085, .025), (.028, .108, .0),
                                                       (.02, .124, -.03), (.012, .133, -.058), (.004, .138, -.073))]
        path = catmull(pts, 8)
        # Links every 7.5 mm, alternately face on and edge on.
        total = sum((path[i + 1] - path[i]).length for i in range(len(path) - 1))
        d, i, k = 0.0, 0, 0
        while d < total:
            while i < len(path) - 2 and sum((path[j + 1] - path[j]).length for j in range(i + 1)) < d:
                i += 1
            t = (path[i + 1] - path[i]).normalized()
            p = path[i] + t * (d - sum((path[j + 1] - path[j]).length for j in range(i)))
            out_n = Vector((sx * .2, 1, .4)).normalized()
            axis = (out_n - t * out_n.dot(t)).normalized() if k % 2 == 0 else t.cross(out_n).normalized()
            torus(pc, p, axis, .0036, .0009, 'nickel', segs=6, rsegs=3)
            d += .0072
            k += 1
    out.append(pc.make(recalc=False))
    pc = Piece('whistle')
    c = Vector((-.006, .147, -.092))
    cyl(pc, c + Vector((-.014, 0, 0)), c + Vector((.012, 0, 0)), .0088, 'nickel-line', segs=16)
    box(pc, c + Vector((0, .0, .0075)), (.014, .007, .003), 'slot')
    mp = Matrix.Rotation(math.radians(-28), 3, 'Y')
    box(pc, c + Vector((-.024, 0, .006)), (.022, .009, .0055), 'nickel-line', rot=mp)
    torus(pc, Vector((-.004, .143, -.079)), (0, 1, 0), .0034, .0009, 'nickel', segs=8, rsegs=4)
    out.append(pc.bevel(.0008, 1, 65).make())
    return out


# ===========================================================================================================================
# The Conductor's lantern (its prop: the pivot at the bail's top, the grip; it hangs down, its glass centred .2 below: Core
# NativeHordeModels.LanternDrop): a railway hand lamp, its wire bail, chimney and vented bell, the red globe in its guard
# wires, the fount below with its brass plate; broken, the globe is a jagged ring of dark glass.
# ===========================================================================================================================
def lantern(broken=False):
    out = []
    pc = Piece('lantern-tin')
    cyl(pc, (0, 0, -.056), (0, 0, -.06), .027, 'tin-dark', segs=16)
    cyl(pc, (0, 0, -.06), (0, 0, -.072), .021, 'tin', segs=16)
    cyl(pc, (0, 0, -.072), (0, 0, -.099), .03, 'tin', segs=20, r1=.058)
    cyl(pc, (0, 0, -.099), (0, 0, -.106), .059, 'tin-dark', segs=24)
    cyl(pc, (0, 0, -.106), (0, 0, -.13), .046, 'tin', segs=20)
    cyl(pc, (0, 0, -.266), (0, 0, -.276), .05, 'tin-dark', segs=20)
    cyl(pc, (0, 0, -.274), (0, 0, -.302), .061, 'tin', segs=24)
    box(pc, (0, .0608, -.288), (.03, .003, .013), 'brass')
    for k in range(8):
        a = k * math.pi / 4
        box(pc, (math.cos(a) * .044, math.sin(a) * .044, -.086), (.006, .006, .01), 'slot', rot=Matrix.Rotation(a, 3, 'Z') @ Matrix.Rotation(math.radians(-35), 3, 'Y'))
    out.append(pc.bevel(.0018, 1, 65).make())
    pc = Piece('lantern-wire')
    tube(pc, [Vector((.058 * math.cos(a), 0, -.062 + .058 * math.sin(a))) for a in [math.pi * i / 16 for i in range(17)]], .0028, 'steel', segs=6)
    for sx in (-1, 1):
        torus(pc, (sx * .058, 0, -.064), (0, 1, 0), .0045, .0014, 'steel', segs=8, rsegs=4)
    for k in range(4):
        a = math.pi / 4 + k * math.pi / 2
        pts = [Vector((math.cos(a) * r, math.sin(a) * r, z)) for r, z in ((.054, -.106), (.06, -.15), (.064, -.2), (.06, -.25), (.054, -.27))]
        tube(pc, catmull(pts, 4), .0025, 'steel', segs=6)
    torus(pc, (0, 0, -.302), (0, 0, 1), .06, .0035, 'tin-dark', segs=24, rsegs=5)
    out.append(pc.make())
    pc = Piece('lantern-globe')
    if not broken:
        sphere(pc, (0, 0, -.2), (.05, .05, .072), 'lantern-glass', u=20, v=12)
        out.append(pc.make())
    else:
        r = bmesh.ops.create_uvsphere(pc.bm, u_segments=20, v_segments=12, radius=1.0)
        for vv in r['verts']:
            vv.co = Vector((vv.co.x * .05, vv.co.y * .05, -.2 + vv.co.z * .072))
        pc.tag(pc.bm.faces, 'glass-dark')
        bmesh.ops.bisect_plane(pc.bm, geom=list(pc.bm.verts) + list(pc.bm.edges) + list(pc.bm.faces), plane_co=Vector((0, 0, -.235)), plane_no=Vector((0, 0, 1)), clear_outer=True)
        rim = [vv for vv in pc.bm.verts if abs(vv.co.z + .235) < 1e-4]
        for k, vv in enumerate(sorted(rim, key=lambda q: math.atan2(q.co.y, q.co.x))):
            vv.co.z += (.026, .006, .018, .0, .022, .01, .03, .004)[k % 8]
        out.append(pc.solidify(.002, -1.0).make(recalc=False))
    return out


# ===========================================================================================================================
# The Undertow (spine_05, both forearms): kelp ribbons and stringy weed draped over its shoulders from the back down the
# chest, a drowned cable with a frayed copper end; weed wound round each forearm, its tails hanging past the wrist.
# ===========================================================================================================================
# Its slicker's outline 9.5 cm out from the spine (zdump undertow-male-a), back to front.
UNDERTOW = [(-.118, -.09), (-.112, -.02), (-.1, .03), (-.08, .07), (-.04, .102), (-.02, .107), (0, .104), (.02, .092), (.04, .078),
            (.06, .06), (.08, .025), (.1, -.006), (.115, -.05), (.118, -.1), (.115, -.15)]


def weeds_back():
    out = []
    pc = Piece('weed-back')
    rnd = [0.37, 0.81, 0.12, 0.64, 0.29, 0.93, 0.55, 0.08, 0.71, 0.46, 0.19, 0.88]
    k = 0
    for sx in (-1, 1):
        for j, x in enumerate((.06, .1, .135)):
            r0, r1 = rnd[k % 12], rnd[(k + 5) % 12]
            pts, nrm = offset_contour(UNDERTOW, .006 + .004 * r0, sx * x)
            pts = pts[int(r1 * 3):]
            nrm = nrm[int(r1 * 3):]
            last = pts[-1]
            tail = [last + Vector((sx * .004, .004, -.05 - .03 * r0)), last + Vector((sx * .01, .002, -.1 - .05 * r1))]
            path = pts + tail
            normals = nrm + [Vector((0, 1, 0))] * 2
            n = len(path)
            path = [q + Vector((.006 * math.sin(i * 1.3 + k), 0, 0)) for i, q in enumerate(path)]
            widths = [(.024 - .006 * j) * (1 - .7 * (i / (n - 1)) ** 2) for i in range(n)]
            ribbon(pc, path, normals, .02, .0022, ('weed', 'weed-olive', 'weed-brown')[(k + j) % 3], widths=widths)
            k += 1
        # Stringy weed: thin strands hanging off the front.
        for j in range(3):
            x = sx * (.07 + .03 * j)
            top = Vector((x, .112, -.05 - .02 * j))
            tube(pc, catmull([top, top + Vector((.004, .01, -.06)), top + Vector((-.006, .006, -.13 - .02 * j))], 4), .0018, 'weed-olive', segs=4)
    # The drowned cable over the left shoulder, frayed at its end.
    pts, nrm = offset_contour(UNDERTOW, .012, -.115)
    path = pts[2:] + [pts[-1] + Vector((-.004, .006, -.06)), pts[-1] + Vector((.0, .004, -.11))]
    tube(pc, catmull(path, 2), .0042, 'cable', segs=8)
    end = path[-1]
    for a in range(5):
        d = Vector((math.cos(a * 1.3) * .006, .004 + .002 * math.sin(a), -.018 - .004 * (a % 2)))
        tube(pc, [end, end + d * .5 + Vector((0, 0, -.004)), end + d], .0009, 'copper', segs=4)
    out.append(pc.make())
    return out


def weeds_arm():
    out = []
    pc = Piece('weed-arm')
    ay, az, r = -.012, -.004, .07
    # A wrap round the forearm.
    wrap = [Vector((.09 + .012 * math.sin(a * 2), ay + (r + .002) * math.cos(a), az + (r + .002) * math.sin(a))) for a in [2 * math.pi * i / 20 for i in range(21)]]
    ribbon(pc, wrap, [Vector((0, math.cos(2 * math.pi * i / 20), math.sin(2 * math.pi * i / 20))) for i in range(21)], .018, .002, 'weed')
    for j, (phi, length, fin) in enumerate(((.4, .2, 'weed-olive'), (1.9, .25, 'weed'), (3.6, .18, 'weed-brown'), (5.0, .23, 'weed'))):
        pts, nrm = [], []
        for i in range(8):
            f = i / 7
            a = phi + .3 * f
            rr = r + .004 + .02 * f
            pts.append(Vector((.09 + length * f, ay + rr * math.cos(a), az + rr * math.sin(a))) + Vector((0, .004 * math.sin(i * 1.7 + j), 0)))
            nrm.append(Vector((0, math.cos(a), math.sin(a))))
        widths = [.02 * (1 - .75 * (i / 7) ** 2) for i in range(8)]
        ribbon(pc, pts, nrm, .02, .002, fin, widths=widths)
    for j in range(2):
        a = 2.6 + j * 2.2
        tube(pc, [Vector((.095, ay + (r + .003) * math.cos(a), az + (r + .003) * math.sin(a))), Vector((.2, ay + (r + .02) * math.cos(a + .2), az + (r + .02) * math.sin(a + .2))),
                  Vector((.29 + .03 * j, ay + (r + .03) * math.cos(a + .3), az + (r + .03) * math.sin(a + .3)))], .0016, 'weed-olive', segs=4)
    out.append(pc.make())
    return out


# ===========================================================================================================================
# The Pileup (spine_05): a carriage's grab pole bent across its chest from the left shoulder round the belly, its mount
# flange still bolted to the top end, the bottom sheared; and its carriage door (its prop: centred on its middle, +z its outer
# face, .7 x 1.26: Core NativeHordeModels.DoorWidth...): stainless panels in their seal, the livery stripe, the window lit by a
# dying tube behind cracked glass, a kick plate, scratches, the release handle, the handrail behind, and the third
# commuter's arm reaching out of the window and clawing down the face.
# ===========================================================================================================================
# The grab pole's path 2.5 cm off the chest (zdumps of pileup-male-a, the wide brute's belly, and pileup-female-a).
RAIL = {
    'm': ((-.17, .045, .085), (-.12, .09, .04), (-.07, .145, .0), (-.03, .172, -.04), (.0, .19, -.065), (.035, .2, -.09), (.07, .207, -.12),
          (.105, .207, -.16), (.13, .212, -.2)),
    'f': ((-.17, .03, .085), (-.12, .085, .04), (-.07, .125, .0), (-.03, .15, -.04), (.0, .175, -.065), (.035, .19, -.09), (.07, .19, -.12),
          (.105, .177, -.16), (.13, .16, -.2)),
}


def handrail(build):
    out = []
    pc = Piece('rail')
    pts = [Vector(p) for p in RAIL[build]]
    tube(pc, catmull(pts, 4), .016, 'rail-yellow', segs=12)
    out.append(pc.make())
    pc = Piece('rail-ends')
    top, nxt = pts[0], pts[1]
    t = (top - nxt).normalized()
    cyl(pc, top - t * .004, top + t * .002, .029, 'stainless-dark', segs=16)
    for k in range(3):
        a = k * 2 * math.pi / 3
        side = t.cross(Vector((0, 0, 1))).normalized()
        up = t.cross(side).normalized()
        q = top + t * .003 + (side * math.cos(a) + up * math.sin(a)) * .02
        cyl(pc, q, q + t * .004, .0035, 'steel', segs=6)
    end, prev = pts[-1], pts[-2]
    t = (end - prev).normalized()
    torus(pc, end, t, .0155, .0025, 'stainless-dark', segs=12, rsegs=4)
    out.append(pc.bevel(.001, 1, 65).make())
    return out


def door():
    out = []
    W, H, T = .7, 1.26, .05
    hw, hh, wy, ww, wh = W / 2, H / 2, .3, .22, .2
    pc = Piece('door-panels')
    box(pc, (0, 0, (-hh + wy - wh) / 2), (W, T, wy - wh + hh), 'stainless')
    box(pc, (0, 0, (hh + wy + wh) / 2), (W, T, hh - wy - wh), 'stainless')
    for sx in (-1, 1):
        box(pc, (sx * (hw + ww) / 2, 0, wy), (hw - ww, T, wh * 2), 'stainless')
    box(pc, (0, T / 2 + .003, -hh + .06), (W * .9, .006, .08), 'stainless-dark')
    box(pc, (0, T / 2 + .003, -.08), (W, .006, .05), 'livery')
    box(pc, (0, T / 2 + .003, -.035), (W, .006, .012), 'cream')
    box(pc, (-.27, T / 2 + .008, .05), (.035, .016, .16), 'stainless-dark')
    box(pc, (0, -T / 2 - .03, -.15), (W * .85, .03, .03), 'stainless')
    for sx in (-1, 1):
        box(pc, (sx * W * .4, -T / 2 - .015, -.15), (.02, .03, .05), 'stainless-dark')
    out.append(pc.bevel(.004, 1, 65).make())
    pc = Piece('door-seal')
    for x, z, sx, sz in ((0, hh + .008, W + .03, .016), (0, -hh - .008, W + .03, .016), (-hw - .008, 0, .016, H + .03), (hw + .008, 0, .016, H + .03),
                         (0, wy + wh + .01, ww * 2 + .03, .02), (0, wy - wh - .01, ww * 2 + .03, .02), (-ww - .01, wy, .02, wh * 2 + .02), (ww + .01, wy, .02, wh * 2 + .02)):
        box(pc, (x, .005, z), (sx, T * .7, sz), 'seal')
    out.append(pc.bevel(.003, 1, 65).make())
    pc = Piece('door-window')
    box(pc, (0, -T * .2, wy), (ww * 2, .01, wh * 2), 'window')
    box(pc, (0, -T * .1, wy + wh - .03), (ww * 1.7, .012, .02), 'tube-light')
    out.append(pc.make())
    pc = Piece('door-marks')
    for path in (((-.12, .35), (-.08, .28), (-.1, .2), (-.04, .14)), ((.02, .4), (.07, .33), (.05, .24)), ((-.15, .45), (-.11, .41))):
        tube(pc, [Vector((x, -T * .2 + .006, z)) for x, z in path], .0012, 'crack', segs=4)
    for k in range(5):
        x = -.26 + k * .13
        tube(pc, [Vector((x, T / 2 + .0015, -.25)), Vector((x + .03 * (k - 2) / 2, T / 2 + .0015, -.59))], .0012, 'scratch', segs=3)
    out.append(pc.make())
    # The third commuter's arm out of the window: the sleeve over the sill, the bare forearm, the hand clawing the face.
    pc = Piece('door-arm')
    sleeve = [Vector((-.05, -.012, wy - .04)), Vector((-.06, .03, wy - .1)), Vector((-.075, .07, wy - .15)), Vector((-.09, .085, wy - .17))]
    tube(pc, catmull(sleeve, 4), .041, 'sleeve', segs=10)
    elbow, wrist = Vector((-.09, .085, wy - .17)), Vector((-.15, .09, wy - .43))
    tube(pc, catmull([elbow, elbow.lerp(wrist, .5) + Vector((0, .006, 0)), wrist], 4), .033, 'dead-skin', segs=10, radii=[.035, .034, .033, .031, .03, .029, .028, .027, .026])
    box(pc, wrist + Vector((-.004, .004, -.05)), (.075, .03, .085), 'dead-skin', rot=Matrix.Rotation(.12, 3, 'Y'))
    for i in range(4):
        x = wrist.x - .033 + i * .022
        k0 = Vector((x, wrist.y + .006, wrist.z - .09))
        tips = [k0, k0 + Vector(((i - 1.5) * .006, .012, -.03)), k0 + Vector(((i - 1.5) * .01, .004, -.06)), k0 + Vector(((i - 1.5) * .012, -.022, -.075))]
        tube(pc, catmull(tips, 3), .0085 - .0004 * i, 'dead-skin', segs=6)
        cyl(pc, tips[-1] + Vector((0, .002, .006)), tips[-1] + Vector((0, -.004, -.002)), .006, 'nail', segs=6)
    thumb = [wrist + Vector((.038, .01, -.04)), wrist + Vector((.05, .018, -.065)), wrist + Vector((.045, -.01, -.085))]
    tube(pc, catmull(thumb, 3), .009, 'dead-skin', segs=6)
    out.append(pc.make())
    return out


def flesh():
    """The fused commuter's flesh (spine_05): over the second head's neck (Runtime ZombieFusedNeck: spine_05 (-.115, .02, .11),
    design (.11, -.02, .115)) and the second arm's shoulder (ZombieFusedShoulder: (-.07, 0, .17), design (.17, 0, .07)), bursting
    out of the jacket's right shoulder: lumps grown together, raw in their creases, veined, a broken collarbone through it,
    the suit torn round its foot."""
    from mathutils import bvhtree, noise
    out = []
    mb = bpy.data.metaballs.new('flesh')
    mb.resolution = .006
    mb.render_resolution = .006
    mb.threshold = .6
    for co, r in (((.11, -.02, .1), .062), ((.155, -.0, .07), .06), ((.13, -.03, .085), .055), ((.085, -.035, .1), .045),
                  ((.17, .02, .05), .045), ((.12, .03, .075), .042), ((.19, -.02, .055), .038), ((.1, .0, .118), .035)):
        e = mb.elements.new(type='BALL')
        e.co = co
        e.radius = r
    mob = bpy.data.objects.new('flesh-mb', mb)
    bpy.context.scene.collection.objects.link(mob)
    deps = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(mob.evaluated_get(deps))
    bpy.data.objects.remove(mob, do_unlink=True)
    bm = bmesh.new()
    bm.from_mesh(me)
    for v in bm.verts:
        p = v.co
        v.co = p + v.normal * (noise.noise(p * 38.0) * .006 + noise.noise(p * 90.0) * .002)
    bm.normal_update()
    bm.to_mesh(me)
    bm.free()
    for poly in me.polygons:
        poly.use_smooth = True
    ob = bpy.data.objects.new('flesh', me)
    bpy.context.scene.collection.objects.link(ob)
    ob.data.materials.append(bpy.data.materials['flesh'])
    out.append(ob)
    deps = bpy.context.evaluated_depsgraph_get()
    tree = bvhtree.BVHTree.FromObject(ob, deps)

    def on(p, lift=.002):
        hit = tree.find_nearest(Vector(p))
        return hit[0] + hit[1] * lift if hit[0] is not None else Vector(p)
    pc = Piece('flesh-veins')
    for path in (((.08, -.05, .11), (.1, -.04, .13), (.13, -.03, .14), (.16, -.01, .12), (.19, .0, .09)),
                 ((.12, .04, .1), (.14, .045, .08), (.16, .04, .06), (.18, .03, .04)),
                 ((.1, -.06, .09), (.12, -.07, .07), (.15, -.06, .06), (.18, -.05, .05)),
                 ((.13, -.01, .145), (.15, .02, .12), (.155, .04, .09))):
        pts = [on(q) for q in catmull([Vector(q) for q in path], 4)]
        tube(pc, pts, .0022, 'vein', segs=5, radii=[.0022 * (1 - .5 * i / (len(pts) - 1)) for i in range(len(pts))])
    out.append(pc.make())
    pc = Piece('flesh-bone')
    base = Vector((.145, -.035, .095))
    tip = base + Vector((.5, -.35, .75)).normalized() * .05
    cyl(pc, base, tip, .0075, 'bone', segs=8, r1=.0055)
    for k in range(5):
        a = k * 2 * math.pi / 5
        d = (tip - base).normalized()
        side = d.cross(Vector((0, 0, 1))).normalized()
        upv = d.cross(side)
        q = tip + (side * math.cos(a) + upv * math.sin(a)) * .005
        cyl(pc, q - d * .002, q + d * (.004 + .004 * (k % 2)), .002, 'bone', segs=4, r1=.0008)
    out.append(pc.make())
    pc = Piece('flesh-cloth')
    for k in range(7):
        a = -.6 + k * .45
        start = on((.13 + .085 * math.cos(a), -.01 + .085 * math.sin(a), .085), .001)
        outward = Vector((math.cos(a), math.sin(a), 0))
        pts = [start, start + outward * .015 + Vector((0, 0, -.006)), start + outward * .028 + Vector((0, 0, -.018))]
        ribbon(pc, pts, [Vector((outward.x, outward.y, .6))] * 3, .022 - .002 * (k % 3), .002, 'suit-torn', widths=[.024, .02, .008])
    out.append(pc.make())
    return out


# ===========================================================================================================================
def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument('--out', required=True, help="a checkout's Assets/LocalAssets")
    ap.add_argument('--preview', help='render each prop here (workbench, its vertex colours)')
    ap.add_argument('--only', default='')
    a = ap.parse_args(argv)
    gk.clear_scene()
    finishes()
    gb = gk.GearBank()
    only = set(x for x in a.only.split(',') if x)
    props = [
        ('linesman-hardhat-yellow', 'head', HEAD, lambda: hardhat('hat-yellow')),
        ('linesman-hardhat-white', 'head', HEAD, lambda: hardhat('hat-white')),
        ('linesman-pack', 'spine_05', CHEST, lambda: pack('m')),
        ('linesman-pack-f', 'spine_05', CHEST, lambda: pack('f')),
        ('linesman-coil', 'lowerarm_r', FOREARM, coil),
        ('conductor-badge', 'head', HEAD, badge),
        ('conductor-whistle', 'spine_05', CHEST, whistle),
        ('conductor-lantern', 'prop', PROP, lantern),
        ('conductor-lantern-broken', 'prop', PROP, lambda: lantern(True)),
        ('undertow-weed-back', 'spine_05', CHEST, weeds_back),
        ('undertow-weed-arm-r', 'lowerarm_r', FOREARM, weeds_arm),
        ('undertow-weed-arm-l', 'lowerarm_l', FOREARM_L, weeds_arm),
        ('pileup-handrail', 'spine_05', CHEST, lambda: handrail('m')),
        ('pileup-handrail-f', 'spine_05', CHEST, lambda: handrail('f')),
        ('pileup-door', 'prop', PROP, door),
        ('pileup-flesh', 'spine_05', CHEST, flesh),
        ('pileup-flesh-f', 'spine_05', CHEST, flesh),
    ]
    for i, (prop, bone, frame, build) in enumerate(props):
        if only and prop not in only:
            continue
        objs = build()
        gm = gk.bake(prop, objs, frame, seed=i * 1.37)
        gb.add(prop, bone, gm)
        if a.preview:
            gk.preview(gm, frame, a.preview, prop)
        for ob in objs:
            bpy.data.objects.remove(ob, do_unlink=True)
    gb.write(a.out, BANK)


if __name__ == '__main__':
    main()
