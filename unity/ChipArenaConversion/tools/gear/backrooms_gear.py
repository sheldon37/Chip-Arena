"""The Backrooms' entities' gear (docs/EXPEDITION-ENEMY-REWORK.md section 4; Core NativeZombieLooks Kit), built with the station's
(station_gear.py main writes them into the same bank). Fits measured from the local tool's zdump, as numbers:
- the face's front in the head bone's frame (FACE: z, the face's +z, on a 15 mm grid of x and y; the eyes' sockets at y .06,
  x +-.045 sink to .077; the nose's tip at y .03 stands at .153);
- the skull as station_gear.py's hard hat has it.
"""
import math

import bmesh
import bpy
from mathutils import Matrix, Vector

import gearkit as gk
from gearkit import Piece, box, catmull, cyl, decal, finish, ribbon, sphere, torus, tube

HEAD = Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
CHEST = Matrix(((0, 0, -1, 0), (0, -1, 0, 0), (1, 0, 0, 0), (0, 0, 0, 1)))
PROP = HEAD


def finishes():
    finish('skin', 0xC0B7A6, 'cloth')
    finish('lanyard', 0x2A4E8A, 'cloth')
    finish('card', 0xEDEBE4, 'sticker')
    finish('card-photo', 0x8A98A8, 'sticker', outline=False)
    finish('card-ink', 0x2A2A2E, 'sticker', outline=False)
    finish('card-red', 0xB8322A, 'sticker', outline=False)
    finish('sleeve-clear', 0xC8D4D8, 'plastic')
    finish('clip', 0x9AA0A4, 'metal', outline=False)
    finish('void', 0x050506, 'cloth', outline=False, dirt=0)
    finish('cap-white', 0xE8E6DC, 'rubber')
    finish('cap-red', 0xB83A32, 'rubber')
    finish('goggle-rim', 0x1A1A1C, 'rubber')
    finish('goggle-lens', 0xA8B8B4, 'plastic')
    finish('cord-red', 0xC0302A, 'cloth')
    finish('whistle-red', 0xD0402A, 'plastic')
    finish('pole', 0xB4BCC0, 'metal')
    finish('pole-grip', 0x1C1C1E, 'rubber')
    finish('pole-tape', 0xC8302A, 'sticker', outline=False)


# ===========================================================================================================================
# The Faceling (head, spine_05): smooth skin over its face from brow to chin (a "-skin" piece: Runtime ZombieGear draws it with
# the head's own material and looks), its eyes' sockets filled, its mouth gone, the nose a soft rise; the lanyard and ID card.
# ===========================================================================================================================
FX = [-.09, -.075, -.06, -.045, -.03, -.015, 0, .015, .03, .045, .06, .075, .09]
FY = [-.045, -.03, -.015, 0, .015, .03, .045, .06, .075, .09, .105, .12]
FACE = [  # z (head frame, m) by FY rows and FX columns; None outside the face
    [None, None, None, .002, None, .118, .119, .118, .058, .046, .022, None, None],
    [None, None, .009, None, .111, .12, .122, .12, .111, .05, .022, None, None],
    [None, None, .09, .109, .119, .126, .128, .126, .119, .109, .09, None, None],
    [None, None, .099, .115, .125, .134, .138, .134, .125, .115, .099, None, None],
    [None, .056, .108, .117, .125, .136, .149, .136, .125, .117, .108, .073, None],
    [.031, .084, .106, .117, .125, .143, .153, .143, .125, .117, .106, .084, .031],
    [.031, .095, .106, .114, .117, .128, .142, .128, .117, .114, .106, .095, .031],
    [.063, .099, .101, .077, .114, .128, .13, .128, .114, .077, .101, .099, .063],
    [.084, .108, .113, .124, .128, .132, .132, .132, .128, .124, .113, .108, .084],
    [.084, .108, .117, .124, .126, .132, .134, .132, .126, .124, .117, .108, .084],
    [.083, .107, .107, .101, .101, None, .134, None, .101, .101, .107, .107, .083],
    [.078, .107, .116, .119, .125, .128, .13, .128, .125, .119, .116, .107, .078],
]


def _face(x, y):
    """The face's front at (x, y), bilinear over FACE (holes from their neighbours)."""
    def at(i, j):
        v = FACE[j][i]
        if v is not None:
            return v
        near = [FACE[j2][i2] for j2 in range(max(0, j - 1), min(len(FY), j + 2)) for i2 in range(max(0, i - 1), min(len(FX), i + 2)) if FACE[j2][i2] is not None]
        return sum(near) / len(near) if near else .0
    fx = max(0, min(len(FX) - 1.0001, (x - FX[0]) / .015))
    fy = max(0, min(len(FY) - 1.0001, (y - FY[0]) / .015))
    i, j = int(fx), int(fy)
    tx, ty = fx - i, fy - j
    return (at(i, j) * (1 - tx) + at(i + 1, j) * tx) * (1 - ty) + (at(i, j + 1) * (1 - tx) + at(i + 1, j + 1) * tx) * ty


def mask():
    pc = Piece('faceling-mask')
    step = .0055
    rx, ry, cy = .082, .079, .036               # the mask's outline: brow to chin, cheek to cheek
    nx, ny = int(2 * rx / step) + 1, int(2 * ry / step) + 1
    pts = {}
    for i in range(nx):
        for j in range(ny):
            x, y = -rx + i * step, cy - ry + j * step
            e = (x / rx) ** 2 + ((y - cy) / ry) ** 2
            if e > 1.0:
                continue
            # A broad blur fills the sockets and the mouth; the mask never sinks under the face (the nose a soft rise).
            acc, w = 0.0, 0.0
            for dx in (-.03, -.015, 0, .015, .03):
                for dy in (-.03, -.015, 0, .015, .03):
                    k = math.exp(-(dx * dx + dy * dy) / (2 * .016 ** 2))
                    acc += _face(x + dx, y + dy) * k
                    w += k
            z = max(_face(x, y) + .0025, acc / w + .004)
            # Its rim tucks under the face's own skin.
            edge = max(0.0, (math.sqrt(e) - .82) / .18)
            z = z * (1 - edge) + (_face(x, y) - .004) * edge
            pts[i, j] = pc.bm.verts.new(Vector((x, z, y)))     # design: X right, Y forward, Z up
    faces = []
    for (i, j), a in pts.items():
        if (i + 1, j) in pts and (i + 1, j + 1) in pts and (i, j + 1) in pts:
            faces.append(pc.bm.faces.new((a, pts[i, j + 1], pts[i + 1, j + 1], pts[i + 1, j])))
    pc.tag(faces, 'skin')
    for f in faces:
        f.normal_update()
        if f.normal.y < 0:
            f.normal_flip()
    pc.sharp = 80
    return [pc.make(recalc=False)]


def lanyard():
    out = []
    pc = Piece('lanyard')
    for sx in (-1, 1):
        pts = [Vector((sx * x, y, z)) for x, y, z in ((.068, .0, .112), (.06, .048, .074), (.046, .07, .045), (.032, .094, .015),
                                                       (.02, .116, -.02), (.01, .13, -.05), (.003, .137, -.068))]
        path = catmull(pts, 4)
        ribbon(pc, path, [Vector((sx * .25, 1, .35))] * len(path), .011, .0018, 'lanyard')
    out.append(pc.make())
    pc = Piece('lanyard-card')
    top = Vector((0, .141, -.074))
    box(pc, top + Vector((0, 0, -.006)), (.012, .006, .012), 'clip')
    tilt = Matrix.Rotation(math.radians(-6), 3, 'X')
    centre = top + Vector((0, .002, -.058))
    box(pc, centre, (.058, .004, .088), 'sleeve-clear', rot=tilt)

    def card(a, b):
        if -.42 < a < -.02 and -.05 < b < .38:
            return 'card-photo'
        if b > .4:
            return 'card-red'
        if a > .05 and any(abs(b - y) < .03 for y in (.3, .18, .06)) and a < .4:
            return 'card-ink'
        if -.42 < a < .42 and abs(b + .25) < .03:
            return 'card-ink'
        return 'card'
    n = tilt @ Vector((0, 1, 0))
    decal(pc, centre + n * .0024, Vector((1, 0, 0)), tilt @ Vector((0, 0, 1)), .052, .08, .003, card)
    out.append(pc.make(recalc=False))
    return out


# ===========================================================================================================================
# The Drowned (head): a vintage rubber swim cap over its bald skull, raised rubber petals on it and a rolled rim, over the ears;
# round goggles over the eyes, fogged, their strap round the cap.
# ===========================================================================================================================
SC = Vector((0, .025, .085))                       # the skull's centre (design: X right, Y forward, Z up)
SR = Vector((.103, .116, .134))                     # the cap: 4 mm over the skull


def _capcut(y):
    return .052 + .26 * (y - SC.y)


def cap(colour):
    out = []
    pc = Piece('cap')
    r = bmesh.ops.create_uvsphere(pc.bm, u_segments=40, v_segments=20, radius=1.0)
    for v in r['verts']:
        x, y, z = v.co
        if z >= 0:
            v.co = SC + Vector((x * SR.x, y * SR.y, z * SR.z))
        else:
            h = math.hypot(x, y)
            d = (x / h, y / h) if h > 1e-6 else (0.0, 0.0)
            v.co = SC + Vector((d[0] * SR.x, d[1] * SR.y, z * .1))
    pc.tag(pc.bm.faces, colour)
    bmesh.ops.bisect_plane(pc.bm, geom=list(pc.bm.verts) + list(pc.bm.edges) + list(pc.bm.faces), plane_co=Vector((0, SC.y, _capcut(SC.y))),
                           plane_no=Vector((0, -.26, 1)).normalized(), clear_inner=True)
    out.append(pc.solidify(.0022, 1.0).make(recalc=False))
    pc = Piece('cap-rim')
    rim = []
    for i in range(48):
        t = 2 * math.pi * i / 48
        d = Vector((math.sin(t), math.cos(t), 0))
        y = SC.y + d.y * (SR.y + .0022)
        rim.append(Vector((d.x * (SR.x + .0022), y, _capcut(y) + .002)))
    tube(pc, rim, .003, colour, segs=6, closed=True)
    out.append(pc.make())
    # Raised petals: rosettes of five rubber discs over the crown and sides.
    pc = Piece('cap-petals')
    for k, (th, el) in enumerate(((0, 1.2), (1.2, .9), (2.4, .9), (3.6, .9), (4.8, .9), (.6, .45), (1.8, .45), (3.0, .45), (4.2, .45), (5.4, .45))):
        dirv = Vector((math.cos(el) * math.sin(th), math.cos(el) * math.cos(th), math.sin(el)))
        p = SC + Vector((dirv.x * SR.x, dirv.y * SR.y, dirv.z * SR.z))
        if p.z < _capcut(p.y) + .02:
            continue
        nrm = Vector(((p.x - SC.x) / SR.x ** 2, (p.y - SC.y) / SR.y ** 2, (p.z - SC.z) / SR.z ** 2)).normalized()
        side = nrm.cross(Vector((0, 0, 1)))
        side = side.normalized() if side.length > 1e-4 else Vector((1, 0, 0))
        up = nrm.cross(side)
        for m in range(5):
            a = m * 2 * math.pi / 5 + k
            q = p + (side * math.cos(a) + up * math.sin(a)) * .0085 + nrm * .002
            sphere(pc, q, (.0062, .0062, .0062), colour, u=8, v=5)
        sphere(pc, p + nrm * .0035, (.004, .004, .004), colour, u=8, v=5)
    out.append(pc.make())
    return out


def goggles():
    out = []
    pc = Piece('goggles')
    for sx in (-1, 1):
        c = Vector((sx * .036, .121, .063))
        a = Vector((sx * .18, 1, 0)).normalized()
        cyl(pc, c - a * .004, c + a * .011, .0205, 'goggle-rim', segs=18)
        torus(pc, c + a * .011, a, .019, .0028, 'goggle-rim', segs=18, rsegs=6)
        cyl(pc, c + a * .009, c + a * .0125, .0168, 'goggle-lens', segs=18)
    tube(pc, [Vector((-.018, .131, .066)), Vector((0, .135, .07)), Vector((.018, .131, .066))], .0022, 'goggle-rim', segs=5)
    band = [Vector((.054, .115, .064))]
    for i in range(40):
        t = math.radians(60 + i * (240 / 39))
        band.append(Vector((math.sin(t) * (SR.x + .006), SC.y + math.cos(t) * (SR.y + .008), .066)))
    band.append(Vector((-.054, .115, .064)))
    ribbon(pc, band, [Vector((q.x, q.y - SC.y, 0)) for q in band], .011, .0018, 'goggle-rim')
    out.append(pc.make())
    return out


# ===========================================================================================================================
# The Lifeguard (spine_05, its right hand): the whistle on a red cord; the rescue hook, an aluminium pole with a shepherd's
# crook, its rubber grip in the fist (the prop's origin: the hand's weapon frame), red tape bands.
# ===========================================================================================================================
def whistle_cord():
    out = []
    pc = Piece('cord')
    for sx in (-1, 1):
        pts = [Vector((sx * x, y, z)) for x, y, z in ((.07, .0, .11), (.062, .045, .075), (.05, .065, .048), (.036, .09, .018), (.024, .114, -.018),
                                                       (.012, .128, -.05), (.004, .134, -.07))]
        tube(pc, catmull(pts, 4), .0026, 'cord-red', segs=5)
    out.append(pc.make())
    pc = Piece('whistle-red')
    c = Vector((-.004, .143, -.088))
    cyl(pc, c + Vector((-.014, 0, 0)), c + Vector((.012, 0, 0)), .0088, 'whistle-red', segs=14)
    box(pc, c + Vector((-.024, 0, .006)), (.022, .009, .0055), 'whistle-red', rot=Matrix.Rotation(math.radians(-28), 3, 'Y'))
    torus(pc, Vector((-.004, .139, -.076)), (0, 1, 0), .0034, .001, 'clip', segs=8, rsegs=4)
    out.append(pc.bevel(.0008, 1, 65).make())
    return out


def hook():
    """Along the fist's grip (the hand's x: across the fingers), through the palm 7 cm down the fingers from the wrist and 2 cm
    out: the pole's grip there, its crook 1.2 m along, turned back."""
    out = []
    g = Vector((0, .02, -.07))                      # the palm's centre from the wrist (design: X across, Y out, Z down the arm)
    def P(x, y=0.0):
        return g + Vector((x, y, 0))
    pc = Piece('pole')
    tube(pc, [P(-.45), P(1.2)], .016, 'pole', segs=10)
    crook = [P(1.17)] + [g + Vector((1.2 + .16 * math.sin(a), 0, .16 - .16 * math.cos(a))) for a in [math.pi * i / 12 for i in range(13)]] + [g + Vector((1.11, 0, .32))]
    tube(pc, catmull(crook, 2), .014, 'pole', segs=10)
    out.append(pc.make())
    pc = Piece('pole-grip')
    cyl(pc, P(-.12), P(.14), .0205, 'pole-grip', segs=12)
    for x in (-.12, .14):
        torus(pc, P(x), (1, 0, 0), .02, .003, 'pole-grip', segs=12, rsegs=4)
    for x in (.45, .7, .95):
        cyl(pc, P(x), P(x + .05), .0172, 'pole-tape', segs=10)
    cyl(pc, P(-.47), P(-.44), .019, 'pole-grip', segs=10)
    out.append(pc.make())
    return out


def props():
    """(id, bone, frame, build) for the bank, after finishes()."""
    finishes()
    return [
        ('faceling-mask-skin', 'head', HEAD, mask),
        ('faceling-lanyard', 'spine_05', CHEST, lanyard),
        ('drowned-cap-white', 'head', HEAD, lambda: cap('cap-white')),
        ('drowned-cap-red', 'head', HEAD, lambda: cap('cap-red')),
        ('drowned-goggles', 'head', HEAD, goggles),
        ('lifeguard-whistle', 'spine_05', CHEST, whistle_cord),
        ('lifeguard-hook', 'prop', PROP, hook),
    ] + __import__('hound_gear').props(HEAD)
