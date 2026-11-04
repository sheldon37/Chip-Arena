"""The City Ironworks kinds' gear (docs/EXPEDITION-CITY-IRONWORKS.md section 6, docs/ZOMBIE-MODELS.md 4n; Core NativeZombieLooks Kit), modelled in
Blender and written as its own local bank Resources/ChipArenaPackIronworks (gearkit.py: the bake, the bone frames, the bank).

usage: blender --background --factory-startup --python tools/gear/ironworks_gear.py -- --out ASSETS/LocalAssets [--preview DIR] [--only id,...]

Its own bank, not ChipArenaPackGear: station_gear.py rewrites that file whole from its own props. Runtime ZombieGearAssetOf reads both.

Fits are numbers measured from the local tool's zdump of each kind's looks (see the tables below); no mesh of the bake is read:
- the head as station_gear.py has it (every variant wears the same head piece): the skull spans x +-.095, z -.083 to .134, its crown at y .215;
- each worn prop rides one bone; a carried prop's frame is the voxel prop's (Core NativeHordeModels: x across, y up, z along it, the grip at the origin),
  so the motion places either.
"""
import argparse
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector, noise

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import converter_gear as cg  # noqa: E402
import gearkit as gk  # noqa: E402
from gearkit import Piece, box, catmull, cyl, decal, finish, ribbon, sphere, torus, tube  # noqa: E402

try:
    from ironworks_fits import FITS  # noqa: E402
except ImportError:
    FITS = {}

BANK = 'ChipArenaPackIronworks'
HEAD = Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))        # design X right, Y forward, Z up to head: x right, y up, z forward
CHEST = Matrix(((0, 0, -1, 0), (0, -1, 0, 0), (1, 0, 0, 0), (0, 0, 0, 1)))     # spine_05: x down, y back, z right
PROP = HEAD


# ===========================================================================================================================
# Finishes: dark shapes, cold tells (never the environment's plain orange: the doc's 3.2)
# ===========================================================================================================================
def finishes():
    # burned
    finish('char', 0x14100E, 'rubber', dirt=.4)
    finish('char-deep', 0x070605, 'rubber', outline=False, dirt=0)
    finish('ash', 0x4C443E, 'cloth', dirt=.5)
    finish('char-bone', 0x2A241F, 'plastic')
    finish('char-rag', 0x1C1612, 'cloth')
    finish('coal-white', 0xFFF4DC, 'glow')
    finish('coal-hot', 0xFFD79A, 'glow')
    finish('ember', 0xFFB66A, 'glow')
    finish('leather-burnt', 0x241912, 'cloth')
    finish('goggle-melt', 0x0C0C0E, 'plastic')
    finish('brass-burnt', 0x4E3C20, 'metal')
    # the heat suit
    finish('foil', 0xBDBAB1, 'metal', dirt=.7)
    finish('foil-dark', 0x7E7C76, 'metal', dirt=.8)
    finish('quilt', 0x8E8B83, 'cloth', dirt=.7)
    finish('plate', 0x0E1114, 'plastic')
    finish('visor', 0x5CE8FF, 'glow')
    finish('visor-dim', 0x2B7D90, 'glow')
    finish('grille', 0x1C1E20, 'metal')
    finish('canister', 0x25282A, 'paint', under=0x7A7C78)
    finish('hose', 0x18181A, 'rubber')
    finish('gauge', 0xE4DFCE, 'sticker', outline=False)
    finish('gauge-ink', 0x15151A, 'sticker', outline=False)
    finish('screw', 0x9A9892, 'metal', outline=False)
    finish('strap', 0x201D1A, 'cloth')
    finish('buckle', 0x8A8A84, 'metal')
    finish('hazard-y', 0xD8B020, 'sticker', outline=False)
    finish('hazard-k', 0x15151A, 'sticker', outline=False)
    finish('led-cyan', 0x5CE8FF, 'glow')
    # iron and steel
    finish('iron', 0x2C2926, 'metal')
    finish('iron-edge', 0x4F4B46, 'metal')
    finish('steel', 0x5A5751, 'metal')
    finish('steel-worn', 0x7E7A72, 'metal')
    finish('grip-wrap', 0x3A2618, 'cloth')
    finish('grip-wrap-dark', 0x24170E, 'cloth')
    finish('heat-blue', 0x35404E, 'metal')
    finish('rivet', 0x5E5A54, 'metal', outline=False)
    # leather and the ladle
    finish('leather', 0x5E3D25, 'cloth')
    finish('leather-dark', 0x342112, 'cloth')
    finish('spatter', 0x231A12, 'sticker', outline=False)
    finish('lens-cyan', 0x5CE8FF, 'glow')
    finish('lens-rim', 0x0E1114, 'plastic')
    finish('crucible', 0x24211E, 'metal')
    finish('refractory', 0x6A5A4A, 'cloth', dirt=.6)
    finish('molten', 0xFFB04A, 'glow')
    finish('molten-core', 0xFFF0C0, 'glow')
    finish('molten-skin', 0xFF8A30, 'glow')
    finish('cold-slag', 0x0C0A09, 'plastic')
    # the rigger
    finish('hat-cream', 0xD2C8AD, 'paint', under=0x8F866E)
    finish('hat-cream-worn', 0xB7AB8A, 'paint', under=0x6F6A58)
    finish('hat-band', 0x1C1C1E, 'cloth')
    finish('harness', 0x2B2C2E, 'plastic')
    finish('lamp-body', 0x2E3236, 'plastic')
    finish('chrome', 0x8A8E90, 'metal')
    finish('lens', 0x5E6264, 'plastic')
    finish('crack', 0x1A1A18, 'sticker', outline=False)
    finish('cable', 0x16161A, 'rubber')
    finish('switch', 0x8C1E18, 'rubber', outline=False)
    finish('decal-black', 0x16161A, 'sticker', outline=False)
    finish('decal-yellow', 0xD8B020, 'sticker', outline=False)
    finish('reflect', 0xD8DDE0, 'sticker', outline=False)
    finish('webbing', 0x24262B, 'cloth')
    finish('webbing-edge', 0x3A3C40, 'cloth')
    finish('hook-paint', 0xC8A020, 'paint', under=0x4F4B46)
    finish('hook-black', 0x15130F, 'paint', under=0x4F4B46)
    finish('lamp-lit', 0xE4EEFF, 'glow')
    finish('lamp-core', 0xFFFFFF, 'glow')
    # slag
    finish('slag', 0x131010, 'plastic', dirt=.35)
    finish('slag-edge', 0x2A2427, 'plastic', dirt=.3)
    finish('slag-crack', 0x8A3014, 'glow')
    finish('fissure', 0xFFD9A0, 'glow')
    finish('fissure-rim', 0xFF9A4A, 'glow')
    finish('raw', 0x2A1812, 'flesh')


# ===========================================================================================================================
# Shared shapes
# ===========================================================================================================================
def rock(pc, centre, radii, rot, fin, seed, sub=2, lump=.28, flat=0.0):
    """A faceted lump: an icosphere scaled to `radii`, roughened by noise (seed its place), its underside (-Z local) flattened by `flat`."""
    r = bmesh.ops.create_icosphere(pc.bm, subdivisions=sub, radius=1.0)
    off = Vector((seed * 3.7, seed * 1.9, seed * 5.3))
    for v in r['verts']:
        d = v.co.normalized()
        n = noise.noise(d * 2.3 + off) * .75 + noise.noise(d * 5.1 + off) * .35
        p = d * (1 + lump * n)
        if flat > 0 and p.z < 0:
            p.z *= (1 - flat)
        v.co = Vector((p.x * radii[0], p.y * radii[1], p.z * radii[2]))
    m = Matrix.Translation(Vector(centre)) @ (rot.to_4x4() if rot is not None else Matrix())
    bmesh.ops.transform(pc.bm, matrix=m, verts=r['verts'])
    faces = set()
    for v in r['verts']:
        faces.update(v.link_faces)
    return pc.tag(list(faces), fin)


def along(n, up=Vector((0, 0, 1))):
    """A rotation taking local +Z to the direction n (local +X level)."""
    n = Vector(n).normalized()
    x = Vector(up).cross(n)
    if x.length < 1e-4:
        x = Vector((1, 0, 0))
    x.normalize()
    y = n.cross(x).normalized()
    return Matrix((x, y, n)).transposed()


def rivets(pc, pts, axis, r=.0035, h=.0028, fin='rivet'):
    a = Vector(axis).normalized()
    for p in pts:
        cyl(pc, Vector(p) - a * .0008, Vector(p) + a * h, r, fin, segs=6, r1=r * .62)


def ellipse(cx, cy, rx, ry, z, n, a0=0.0, a1=2 * math.pi, closed=True):
    span = (a1 - a0) / (n if closed else n - 1)
    return [Vector((cx + rx * math.sin(a0 + i * span), cy + ry * math.cos(a0 + i * span), z)) for i in range(n)]


def band_z(pc, loop_lo, loop_hi, fin, outward_c=None):
    """A strip between two rings of points (matching counts), wound outward from `outward_c` (a point inside)."""
    lo = [pc.bm.verts.new(p) for p in loop_lo]
    hi = [pc.bm.verts.new(p) for p in loop_hi]
    n = len(lo)
    faces = [pc.bm.faces.new((lo[i], lo[(i + 1) % n], hi[(i + 1) % n], hi[i])) for i in range(n)]
    pc.tag(faces, fin)
    if outward_c is not None:
        for f in faces:
            f.normal_update()
            if f.normal.dot(f.calc_center_median() - Vector(outward_c)) < 0:
                f.normal_flip()
    return faces


# ===========================================================================================================================
# Cinderling: the skull (head) split by a white-hot crack, plates of burned scalp lifting off it; or a welder's cap and goggles melted onto it
# ===========================================================================================================================
SKULL_C = Vector((0, .025, .105))
SKULL_R = Vector((.1, .116, .116))


def onskull(p, off=0.0):
    q = Vector(p) - SKULL_C
    s = 1 / math.sqrt((q.x / SKULL_R.x) ** 2 + (q.y / SKULL_R.y) ** 2 + (q.z / SKULL_R.z) ** 2)
    at = SKULL_C + q * s
    n = Vector(((at.x - SKULL_C.x) / SKULL_R.x ** 2, (at.y - SKULL_C.y) / SKULL_R.y ** 2, (at.z - SKULL_C.z) / SKULL_R.z ** 2)).normalized()
    return at + n * off, n


def cinder_skull():
    out = []
    # The burned scalp: overlapping charred plates round the crown, left open along a split from the brow to the back.
    pc = Piece('scalp')
    for i, (th, ph, rx, ry, seed) in enumerate(((-.9, .55, .05, .06, 1.1), (-1.5, .95, .045, .055, 2.3), (-.55, 1.2, .055, .05, 3.7), (.95, .6, .05, .055, 4.2), (1.45, 1.0, .045, .06, 5.9),
                                                (.5, 1.25, .05, .05, 6.4), (2.5, .85, .06, .055, 7.8), (-2.6, .95, .055, .05, 8.1), (3.05, 1.35, .045, .05, 9.6), (-1.95, .35, .04, .04, 10.2), (1.9, .4, .04, .035, 11.5))):
        d = Vector((math.sin(th) * math.cos(ph), math.cos(th) * math.cos(ph), math.sin(ph)))
        at, n = onskull(SKULL_C + d, .002)
        rock(pc, at, (rx, ry, .012), along(n), 'char' if i % 3 else 'ash', seed, sub=2, lump=.34, flat=.6)
    pc.sharp = 40
    out.append(pc.make())
    # The split: a jagged white-hot seam through the plates, branching over the temple; its edges glowing cream.
    pc = Piece('split')
    route = [(-.015, .135, .165), (.012, .11, .19), (-.006, .075, .212), (.016, .035, .222), (-.01, -.01, .218), (.014, -.05, .2), (-.008, -.085, .165), (.006, -.1, .125)]
    pts = [onskull(p, .0025)[0] for p in route]
    tube(pc, catmull(pts, 3), .006, 'coal-white', segs=5)
    tube(pc, catmull([onskull(p, .0028)[0] for p in ((.012, .11, .19), (.05, .1, .17), (.075, .085, .135))], 3), .003, 'coal-hot', segs=4)
    tube(pc, catmull([onskull(p, .0028)[0] for p in ((-.01, -.01, .218), (-.055, -.02, .195), (-.085, -.035, .15))], 3), .0034, 'coal-hot', segs=4)
    tube(pc, catmull([onskull(p, .0028)[0] for p in ((.014, -.05, .2), (.05, -.07, .17))], 3), .0025, 'coal-hot', segs=4)
    out.append(pc.make())
    # The split's lips: a dark crust either side.
    pc = Piece('lips')
    for side in (-1, 1):
        tube(pc, catmull([onskull(Vector(p) + Vector((side * .011, 0, 0)), .0035)[0] for p in route], 3), .0052, 'char-deep', segs=5)
    out.append(pc.make())
    return out


def cinder_cap():
    out = []
    # A welder's cap gone to char, fused to the crown: a sagging dome, its peak curled, torn at the back with the coal's light through it.
    pc = Piece('cap')
    r = bmesh.ops.create_uvsphere(pc.bm, u_segments=28, v_segments=14, radius=1.0)
    for v in r['verts']:
        p = v.co.copy()
        n = noise.noise(p * 3.1 + Vector((4.1, 1.2, 7.7))) * .1 + noise.noise(p * 8.7) * .04
        v.co = SKULL_C + Vector((p.x * (SKULL_R.x + .008), p.y * (SKULL_R.y + .008), p.z * (SKULL_R.z + .01))) * (1 + n)
    pc.tag(pc.bm.faces, 'char')
    bmesh.ops.bisect_plane(pc.bm, geom=list(pc.bm.verts) + list(pc.bm.edges) + list(pc.bm.faces), plane_co=Vector((0, 0, .135)), plane_no=Vector((0, -.08, 1)).normalized(), clear_inner=True)
    pc.sharp = 50
    out.append(pc.solidify(.004, 1.0).make(recalc=False))
    pc = Piece('peak')
    pts = []
    for i in range(9):
        t = -1.0 + 2.0 * i / 8
        y = .142 + .012 * (1 - t * t)
        pts.append((Vector((t * .085, y - .002, .147 - .012 * t * t)), Vector((t * .075, y + .05 - .012 * t * t, .118 - .022 * t * t - .01 * t))))
    verts = [(pc.bm.verts.new(a), pc.bm.verts.new(b)) for a, b in pts]
    faces = [pc.bm.faces.new((verts[i][0], verts[i][1], verts[i + 1][1], verts[i + 1][0])) for i in range(8)]
    pc.tag(faces, 'char')
    for f in faces:
        f.normal_update()
        if f.normal.z < 0:
            f.normal_flip()
    out.append(pc.solidify(.004, -1.0).make(recalc=False))
    # Goggles melted onto the brow: two lens cups run together, their strap burned to a crust round the back; one lens gone with the coal's glow behind it.
    pc = Piece('goggles')
    for sx, fin in ((-.042, 'goggle-melt'), (.042, 'goggle-melt')):
        c = Vector((sx, .135, .165))
        cyl(pc, c + Vector((0, -.012, 0)), c + Vector((0, .012, .001)), .029, fin, segs=14, r1=.026)
        torus(pc, c + Vector((0, .012, .001)), (0, 1, .02), .0265, .004, 'brass-burnt', segs=14, rsegs=5)
    box(pc, (0, .146, .165), (.03, .012, .011), 'brass-burnt')
    strap = ellipse(0, .012, .107, .124, .155, 30)
    faces = band_z(pc, [p + Vector((0, 0, -.012)) for p in strap], [p + Vector((0, 0, .012)) for p in strap], 'leather-burnt', Vector((0, .012, .155)))
    out.append(pc.solidify(.003, 1.0).make(recalc=False))
    pc = Piece('lens-glow')
    cyl(pc, (.042, .147, .166), (.042, .1485, .1665), .021, 'coal-hot', segs=12)
    cyl(pc, (-.042, .1475, .165), (-.042, .149, .1655), .02, 'char-deep', segs=12)
    out.append(pc.make())
    # Cracks up the skull below the cap's edge.
    pc = Piece('cracks')
    tube(pc, catmull([onskull(p, .0022)[0] for p in ((.09, .06, .135), (.094, .02, .11), (.087, -.02, .085))], 3), .0032, 'coal-white', segs=4)
    tube(pc, catmull([onskull(p, .0022)[0] for p in ((-.09, -.03, .12), (-.09, -.065, .095))], 3), .0028, 'coal-hot', segs=4)
    out.append(pc.make())
    return out


# ===========================================================================================================================
# The Stoker's square hood (head): stiff aluminised cloth over a frame, one black face plate with the cyan visor slit (the tell, its weak point), a grille and
# filter port under it, quilted seams, the neck skirt; the dome: a rounder hood with a short brim.
# ===========================================================================================================================
HOOD_W, HOOD_FRONT, HOOD_BACK, HOOD_TOP, HOOD_LOW = .138, .168, -.122, .29, -.055


def hood(dome=False):
    out = []
    w, fr, bk, tp, lo = HOOD_W, HOOD_FRONT, HOOD_BACK, HOOD_TOP, HOOD_LOW
    yc, yd = (fr + bk) / 2, fr - bk
    pc = Piece('hood-body')
    if not dome:
        box(pc, (0, yc, (tp + lo) / 2), (w * 2, yd, tp - lo), 'foil')
        pc.bevel(.026, 3, 40)
    else:
        box(pc, (0, yc, (lo + .17) / 2), (w * 2, yd, .17 - lo), 'foil')
        sphere(pc, (0, yc, .165), (w, yd / 2, .135), 'foil', u=28, v=14)
        pc.bevel(.018, 2, 40)
    out.append(pc.make())
    # Quilted panels stand off the sides, the back and the top; the seams between them run dark.
    pc = Piece('hood-quilt')
    rows = 4
    for side in (-1, 1):
        for i in range(rows):
            z = lo + .045 + i * (.175 if dome else tp - lo - .09) / (rows - 1)
            box(pc, (side * (w + .0015), yc - .01, z), (.006, yd - .07, .012), 'quilt')
        box(pc, (side * (w + .0025), yc - .01, (lo + (.22 if dome else tp - .03)) / 2), (.006, .012, (.22 if dome else tp - .03) - lo - .02), 'foil-dark')
    for i in range(rows):
        z = lo + .045 + i * (.175 if dome else tp - lo - .09) / (rows - 1)
        box(pc, (0, bk - .0015, z), (w * 2 - .05, .006, .012), 'quilt')
    if not dome:
        for x in (-.07, 0, .07):
            box(pc, (x, yc, tp + .0015), (.012, yd - .06, .006), 'quilt')
        box(pc, (0, fr - .022, tp + .002), (w * 2 - .05, .012, .008), 'foil-dark')
    out.append(pc.bevel(.002, 1, 50).make())
    # The face plate, its frame, the visor slit, the grille and filter port, rivets.
    fy = fr + .001
    pc = Piece('hood-face')
    box(pc, (0, fy, .083), (.226, .012, .23), 'plate')
    for z, sx, sz in ((.2, .232, .014), (-.034, .232, .014)):
        box(pc, (0, fy + .004, z), (sx, .014, sz), 'foil-dark')
    for sx in (-1, 1):
        box(pc, (sx * .116, fy + .004, .083), (.014, .014, .232), 'foil-dark')
    # the slit's hood: a little brow over it
    box(pc, (0, fy + .015, .147), (.206, .03, .009), 'plate', rot=Matrix.Rotation(math.radians(-14), 3, 'X'))
    # the grille: slats across an opening, its port and a ring
    box(pc, (0, fy + .005, .032), (.15, .004, .082), 'grille')
    for i in range(5):
        box(pc, (0, fy + .0085, .004 + i * .0145), (.142, .005, .0072), 'iron-edge', rot=Matrix.Rotation(math.radians(30), 3, 'X'))
    out.append(pc.bevel(.0018, 1, 50).make())
    pc = Piece('hood-port')
    pa = Vector((.092, fy + .005, -.012))
    cyl(pc, pa, pa + Vector((.006, .03, -.006)), .028, 'canister', segs=14, r1=.023)
    torus(pc, pa + Vector((.006, .03, -.006)), (.2, 1, -.2), .0215, .0032, 'iron-edge', segs=14, rsegs=5)
    cyl(pc, pa + Vector((.006, .03, -.006)), pa + Vector((.0065, .032, -.0065)), .018, 'plate', segs=12)
    rivets(pc, [(sx * .1, fy + .005, z) for sx in (-1, 1) for z in (.19, -.026)], (0, 1, 0), .004, .003)
    out.append(pc.make())
    pc = Piece('hood-visor')
    box(pc, (0, fy + .0072, .118), (.186, .002, .03), 'visor')
    box(pc, (0, fy + .0078, .118), (.15, .002, .012), 'coal-white')
    out.append(pc.make())
    # The neck skirt: a stiff skirt of foil past the jaw, closed by a drawstring.
    pc = Piece('hood-skirt')
    a = [Vector((math.sin(t) * (w - .004), yc + math.cos(t) * (yd / 2 - .002), lo + .003)) for t in [2 * math.pi * i / 28 for i in range(28)]]
    b = [Vector((math.sin(t) * (w - .022), yc - .002 + math.cos(t) * (yd / 2 - .02), lo - .042)) for t in [2 * math.pi * i / 28 for i in range(28)]]
    band_z(pc, b, a, 'foil-dark', Vector((0, yc, lo - .02)))
    pc2 = Piece('hood-string')
    tube(pc2, [p + Vector((0, 0, .004)) for p in b] + [b[0] + Vector((0, 0, .004))], .0032, 'strap', segs=5)
    out.append(pc.solidify(.003, 1.0).make(recalc=False))
    out.append(pc2.make())
    if dome:
        pc = Piece('hood-brim')
        for i in range(1):
            box(pc, (0, fr + .03, .222), (.25, .07, .012), 'foil', rot=Matrix.Rotation(math.radians(-16), 3, 'X'))
        out.append(pc.bevel(.005, 2, 40).make())
    # A status lamp beside the slit.
    pc = Piece('hood-led')
    box(pc, (.1, fy + .009, .17), (.012, .004, .012), 'led-cyan')
    out.append(pc.make())
    # (All of it drawn in: a hood sits close round the head, not a crate on the shoulders; Core NativeZombieLooks.StokerVisor is the slit's place after this.)
    c = Vector((0, .02, .1))
    m = Matrix.Translation(c) @ Matrix.Scale(.88, 4) @ Matrix.Translation(-c)
    for ob in out:
        ob.matrix_world = m
    return out


# ===========================================================================================================================
# Carried props: the Stoker's rake, the Ladleman's ladle (full and spilled), the crane hook, the lamp's lit lens
# (design X across, Y along the prop from the grip, Z up; Core NativeHordeModels: RakeButt -.55, RakeTip 2.42, LadleBowl .76, radius .28, HookDrop .62)
# ===========================================================================================================================
RAKE_BUTT, RAKE_TIP = -.55, 2.42
LADLE_BOWL, LADLE_R = .76, .28


def rake():
    out = []
    pc = Piece('rake-pole')
    # Round iron pipe in two lengths joined by a sleeve; the butt ring, scorched toward the head.
    cyl(pc, (0, RAKE_BUTT, 0), (0, .95, 0), .0215, 'iron', segs=10)
    cyl(pc, (0, .95, 0), (0, RAKE_TIP - .06, 0), .019, 'heat-blue', segs=10)
    cyl(pc, (0, .9, 0), (0, 1.0, 0), .0255, 'iron-edge', segs=10)
    cyl(pc, (0, RAKE_BUTT - .012, 0), (0, RAKE_BUTT + .018, 0), .027, 'iron-edge', segs=10)
    out.append(pc.bevel(.0025, 1, 50).make())
    pc = Piece('rake-grips')
    for y0, y1 in ((-.24, .3), (.46, .94)):
        cyl(pc, (0, y0, 0), (0, y1, 0), .0262, 'grip-wrap', segs=10)
        n = int((y1 - y0) / .055)
        for i in range(n + 1):
            y = y0 + i * (y1 - y0) / n
            torus(pc, (0, y, 0), (0.0, 1, .12), .0265, .0022, 'grip-wrap-dark', segs=10, rsegs=4)
    out.append(pc.make())
    # The head: a forged crossbar on a socket, gusseted, three tines, the middle one the long hook; the steel heat-blued, its tip still hot.
    y = RAKE_TIP
    pc = Piece('rake-head')
    cyl(pc, (0, y - .16, 0), (0, y - .02, 0), .03, 'iron', segs=10, r1=.024)
    rivets(pc, [(0, y - .12, .029), (0, y - .08, -.028)], (0, 0, 1), .0055, .004)
    box(pc, (0, y - .015, 0), (.33, .05, .06), 'iron-edge')
    for sx in (-1, 1):
        box(pc, (sx * .055, y - .07, 0), (.1, .09, .012), 'iron', rot=Matrix.Rotation(sx * math.radians(38), 3, 'Z'))
    out.append(pc.bevel(.004, 2, 45).make())
    pc = Piece('rake-tines')
    hk = [Vector((0, y, 0)), Vector((0, y + .08, -.01)), Vector((0, y + .135, -.08)), Vector((0, y + .125, -.19)), Vector((0, y + .05, -.25)), Vector((0, y - .03, -.31))]
    path = catmull(hk, 4)
    radii = [max(.0035, .025 * (1 - (i / (len(path) - 1)) ** 2.4)) for i in range(len(path))]
    tube(pc, path, .02, 'heat-blue', segs=8, radii=radii)
    for sx in (-.135, .135):
        side = [Vector((sx, y, 0)), Vector((sx, y + .03, -.03)), Vector((sx, y + .03, -.11)), Vector((sx, y - .01, -.17))]
        path = catmull(side, 3)
        tube(pc, path, .013, 'iron', segs=6, radii=[max(.003, .0145 * (1 - (i / (len(path) - 1)) ** 2)) for i in range(len(path))])
    # a chain from the head's lug back to a ring (the hook's line coils here)
    torus(pc, (0, y - .055, -.042), (1, 0, 0), .016, .004, 'steel', segs=10, rsegs=5)
    out.append(pc.make())
    pc = Piece('rake-hot')
    tip = catmull(hk, 4)[-5:]
    tube(pc, [p + Vector((0, 0, .0)) for p in tip], .0045, 'ember', segs=5, radii=[.0052, .0048, .0042, .0036, .0026])
    out.append(pc.make())
    return out


def bowl_r(z):
    """The crucible's outer radius at height z (ladle()'s profile)."""
    prof = ((.17, -.2), (.225, -.16), (.265, -.04), (.28, .11), (.292, .155))
    if z <= prof[0][1]:
        return prof[0][0]
    for (r0, z0), (r1, z1) in zip(prof, prof[1:]):
        if z <= z1:
            return r0 + (r1 - r0) * (z - z0) / (z1 - z0)
    return prof[-1][0]


def ladle(full=True):
    out = []
    b, R = LADLE_BOWL, LADLE_R
    pc = Piece('ladle-shank')
    # The shank: forged bar with a welded T-grip at the back, a sliding collar at the front grip, its fork to the trunnions.
    cyl(pc, (0, -.40, 0), (0, b - .36, 0), .029, 'iron', segs=10)
    cyl(pc, (0, -.05, 0), (0, .03, 0), .036, 'iron-edge', segs=10)
    cyl(pc, (0, b - .41, 0), (0, b - .34, 0), .038, 'iron-edge', segs=10)
    cyl(pc, (-.21, -.39, 0), (.21, -.39, 0), .026, 'iron', segs=10)
    out.append(pc.bevel(.003, 1, 50).make())
    pc = Piece('ladle-grips')
    for sx in (-1, 1):
        cyl(pc, (sx * .1, -.39, 0), (sx * .215, -.39, 0), .031, 'grip-wrap', segs=10)
        for i in range(4):
            torus(pc, (sx * (.112 + i * .03), -.39, 0), (1, 0, 0), .0315, .0025, 'grip-wrap-dark', segs=10, rsegs=4)
    cyl(pc, (0, -.26, 0), (0, -.1, 0), .034, 'grip-wrap', segs=10)
    for i in range(5):
        torus(pc, (0, -.25 + i * .036, 0), (0, 1, 0), .0345, .0025, 'grip-wrap-dark', segs=10, rsegs=4)
    out.append(pc.make())
    pc = Piece('ladle-fork')
    for sx in (-1, 1):
        fork = [Vector((0, b - .37, 0)), Vector((sx * .1, b - .345, .004)), Vector((sx * .24, b - .24, .018)), Vector((sx * (R + .02), b - .09, .03)), Vector((sx * (R + .03), b, .035))]
        tube(pc, catmull(fork, 4), .021, 'iron', segs=8)
        cyl(pc, (sx * (R - .01), b, .035), (sx * (R + .06), b, .035), .032, 'iron-edge', segs=10)
        cyl(pc, (sx * (R + .06), b, .035), (sx * (R + .068), b, .035), .022, 'steel', segs=10)
    # the yoke's safety latch across the top of the fork
    box(pc, (0, b - .32, .012), (.17, .03, .03), 'iron-edge')
    rivets(pc, [(sx * .07, b - .32, .027) for sx in (-1, 1)], (0, 0, 1), .007, .005)
    out.append(pc.bevel(.0025, 1, 50).make())
    # The crucible: a tapered steel shell lined with refractory, two hoops, a band of trunnion lugs, the pour lip; slag crusted down its side.
    pc = Piece('ladle-bowl')
    prof = [(.17, -.2), (.225, -.16), (.265, -.04), (.28, .11), (.292, .155)]
    n = 32
    rings = []
    for r, z in prof:
        rings.append([pc.bm.verts.new(Vector((math.sin(2 * math.pi * i / n) * r, b + math.cos(2 * math.pi * i / n) * r, z))) for i in range(n)])
    faces = []
    for k in range(len(rings) - 1):
        for i in range(n):
            faces.append(pc.bm.faces.new((rings[k][i], rings[k][(i + 1) % n], rings[k + 1][(i + 1) % n], rings[k + 1][i])))
    faces.append(pc.bm.faces.new(list(reversed(rings[0]))))
    pc.tag(faces, 'crucible')
    for f in faces:
        f.normal_update()
        if f.normal.dot(f.calc_center_median() - Vector((0, b, 0))) < 0:
            f.normal_flip()
    out.append(pc.make(recalc=False))
    pc = Piece('ladle-hoops')
    torus(pc, (0, b, .155), (0, 0, 1), .294, .021, 'iron-edge', segs=32, rsegs=6)
    torus(pc, (0, b, .015), (0, 0, 1), .277, .017, 'iron', segs=32, rsegs=5)
    torus(pc, (0, b, -.15), (0, 0, 1), .232, .014, 'iron', segs=28, rsegs=5)
    rivets(pc, [Vector((math.sin(t) * .279, b + math.cos(t) * .279, .015)) for t in [i * math.pi / 8 for i in range(16)]], (0, 0, 1), .005, .012)
    # the lip: a poured spout at the far side, thick with old metal
    box(pc, (0, b + R + .022, .13), (.11, .1, .05), 'crucible', rot=Matrix.Rotation(math.radians(-20), 3, 'X'))
    for sx in (-1, 1):
        box(pc, (sx * .058, b + R + .026, .15), (.014, .1, .05), 'iron-edge', rot=Matrix.Rotation(math.radians(-20), 3, 'X'))
    out.append(pc.bevel(.0035, 1, 50).make())
    pc = Piece('ladle-crust')
    for i, (t, z, r, seed) in enumerate(((.15, .06, .03, 1.2), (-.2, .02, .036, 2.4), (.05, -.05, .026, 3.3), (.4, -.02, .022, 4.9), (-.45, .08, .024, 5.5))):
        rock(pc, (math.sin(t) * .283, b + math.cos(t) * .283, z), (r, r * .8, r * 1.7), None, 'cold-slag', seed, sub=2, lump=.3)
    pc.sharp = 40
    out.append(pc.make())
    pc = Piece('ladle-lining')
    in_r = .262
    a = [Vector((math.sin(t) * .279, b + math.cos(t) * .279, .172)) for t in [2 * math.pi * i / n for i in range(n)]]
    c = [Vector((math.sin(t) * in_r, b + math.cos(t) * in_r, .148)) for t in [2 * math.pi * i / n for i in range(n)]]
    band_z(pc, a, c, 'refractory', Vector((0, b, .05)))
    d = [Vector((math.sin(t) * (in_r - .012), b + math.cos(t) * (in_r - .012), .08)) for t in [2 * math.pi * i / n for i in range(n)]]
    band_z(pc, c, d, 'refractory', Vector((0, b, .3)))
    out.append(pc.make(recalc=False))
    if full:
        # The metal: a bright bath, its skin darker in patches at the rim, a white-hot heart, bubbles; the brightest thing on any enemy.
        pc = Piece('ladle-melt')
        cyl(pc, (0, b, .1), (0, b, .132), in_r - .006, 'molten', segs=28)
        out.append(pc.make())
        pc = Piece('ladle-core')
        cyl(pc, (0, b, .13), (0, b, .138), .155, 'molten-core', segs=20)
        for i in range(7):
            a2 = i * 2.4
            r2 = .05 + .03 * i
            sphere(pc, (math.cos(a2) * r2 * .9, b + math.sin(a2) * r2 * .9, .139), (.018 + .004 * (i % 3), .018 + .004 * (i % 3), .01), 'molten-core', u=8, v=5)
        for i in range(6):
            a2 = i * 1.05 + .4
            rock(pc, (math.cos(a2) * .2, b + math.sin(a2) * .2, .136), (.036, .022, .005), along((0, 0, 1)) @ Matrix.Rotation(a2, 3, 'Z'), 'molten-skin', i * 1.7, sub=1, lump=.2)
        # The heat in its iron: the lining's rim lit, metal slopped over the spout and run down the shell, the shell's cracks glowing, seen from every side.
        torus(pc, (0, b, .15), (0, 0, 1), in_r + .004, .0085, 'molten', segs=28, rsegs=4)
        spill = [Vector((0, b + in_r - .02, .142)), Vector((0, b + R + .03, .158)), Vector((.008, b + R + .07, .118)), Vector((.012, b + R + .058, .06)), Vector((.02, b + R + .018, .0)), Vector((.012, b + R, -.06)), Vector((.018, b + R - .02, -.11))]
        path = catmull(spill, 3)
        tube(pc, path, .02, 'molten', segs=7, radii=[max(.006, .03 * (1 - i / (len(path) - 1)) ** 1.2) for i in range(len(path))])
        for k, (t0, z0) in enumerate(((.9, .09), (-1.1, .03), (2.3, .07), (-2.6, -.02), (.2, -.09), (3.0, -.1))):
            pts = []
            for i in range(5):
                t = t0 + (i - 2) * .07 * (1 if k % 2 else -1)
                z = z0 - i * .032
                rs = bowl_r(z) + .0035
                pts.append(Vector((math.sin(t) * rs, b + math.cos(t) * rs, z)))
            tube(pc, pts, .0044, 'ember', segs=4)
        out.append(pc.make())
    else:
        # Spilled: a skin of dark metal in the bottom, a dull glow left in its cracks.
        pc = Piece('ladle-dregs')
        rock(pc, (0, b, .045), (in_r - .03, in_r - .03, .03), None, 'cold-slag', 3.1, sub=3, lump=.08, flat=.5)
        pc.sharp = 40
        out.append(pc.make())
        pc = Piece('ladle-dregs-glow')
        for i in range(4):
            a2 = i * 1.6 + .3
            tube(pc, [Vector((math.cos(a2) * .03, b + math.sin(a2) * .03, .074)), Vector((math.cos(a2 + .4) * .13, b + math.sin(a2 + .4) * .13, .076)), Vector((math.cos(a2 + .1) * .19, b + math.sin(a2 + .1) * .19, .07))], .0035, 'molten-skin', segs=4)
        out.append(pc.make())
    return out


def hook():
    """The crane hook (about .6 m): the swivel eye at the origin, a thrust bearing, the shank, the C of the hook open to +Y, a sprung safety latch, yellow paint worn to iron."""
    out = []
    pc = Piece('hook-eye')
    torus(pc, (0, 0, -.06), (1, 0, 0), .055, .017, 'steel', segs=16, rsegs=8)
    cyl(pc, (0, 0, -.115), (0, 0, -.14), .036, 'iron-edge', segs=14)
    cyl(pc, (0, 0, -.14), (0, 0, -.19), .05, 'steel', segs=16, r1=.056)
    cyl(pc, (0, 0, -.19), (0, 0, -.205), .062, 'iron-edge', segs=16)
    rivets(pc, [Vector((math.sin(t) * .052, math.cos(t) * .052, -.19)) for t in [i * math.pi / 3 for i in range(6)]], (0, 0, 1), .005, .004)
    out.append(pc.bevel(.0025, 1, 50).make())
    # The hook's body: a thick forged C that thins to its point, flattened a little as a real one is.
    pc = Piece('hook-body')
    path = [Vector((0, 0, -.2)), Vector((0, 0, -.28))]
    for i in range(0, 15):
        a = math.pi * (1 + 1.32 * i / 14)
        path.append(Vector((0, .1 + .1 * math.cos(a), -.42 + .1 * math.sin(a))))
    path = catmull(path, 2)
    m = len(path)
    radii = [.043 if i < 6 else max(.004, .046 - .042 * ((i - 6) / (m - 7)) ** 1.5) for i in range(m)]
    tube(pc, path, .04, 'hook-paint', segs=12, radii=radii)
    out.append(pc.make())
    # Hazard bands, the safe-load mark and the latch.
    pc = Piece('hook-marks')
    for i in range(3):
        k = int(m * (.3 + i * .14))
        d = (path[k + 1] - path[k]).normalized()
        torus(pc, path[k], d, radii[k] + .0005, .0036, 'hook-black', segs=14, rsegs=4)
    out.append(pc.make())
    pc = Piece('hook-latch')
    a = Vector((0, .018, -.235))
    tip = path[-1] + Vector((0, -.01, .008))
    cyl(pc, a + Vector((-.012, 0, 0)), a + Vector((.012, 0, 0)), .009, 'iron-edge', segs=8)
    mid = (a + tip) / 2
    d = tip - a
    box(pc, mid, (.022, d.length, .006), 'steel-worn', rot=Vector((0, 1, 0)).rotation_difference(d.normalized()).to_matrix())
    tube(pc, catmull([a + Vector((0, 0, .0)), a + Vector((0, .04, -.016)), a + Vector((0, .08, -.018))], 3), .0032, 'steel', segs=5)
    out.append(pc.make())
    return out


def lamp_lit():
    pc = Piece('lamp')
    cyl(pc, (0, 0, 0), (0, .002, 0), .0168, 'lamp-lit', segs=16)
    cyl(pc, (0, .002, 0), (0, .003, 0), .009, 'lamp-core', segs=12)
    return [pc.make()]


# ===========================================================================================================================
# The Ladleman's mask (head): a riveted iron plate curved round the face, one cyan lens in a hinged cover, heavy brow, its straps round the back of the skull
# ===========================================================================================================================
def ladle_mask():
    out = []
    pc = Piece('mask-plate')
    # A curved plate: columns round the face's front, cut under the chin.
    n, m = 22, 12
    grid = []
    for i in range(n + 1):
        t = -1 + 2 * i / n
        ang = t * math.radians(82)
        col = []
        for j in range(m + 1):
            z = -.04 + j * (.235 + .04) / m
            k = 1 - .11 * ((z - .085) / .14) ** 2
            rx, ry = .119 * k, .152 * k
            y0 = .02
            col.append(pc.bm.verts.new(Vector((math.sin(ang) * rx, y0 + math.cos(ang) * (ry - y0 + .01), z))))
        grid.append(col)
    faces = []
    for i in range(n):
        for j in range(m):
            faces.append(pc.bm.faces.new((grid[i][j], grid[i + 1][j], grid[i + 1][j + 1], grid[i][j + 1])))
    pc.tag(faces, 'iron')
    for f in faces:
        f.normal_update()
        if f.normal.dot(f.calc_center_median() - Vector((0, 0, .08))) < 0:
            f.normal_flip()
    out.append(pc.solidify(.006, 1.0).bevel(.0018, 1, 50).make(recalc=False))

    def face(th, z, off=0.0):
        k = 1 - .11 * ((z - .085) / .14) ** 2
        rx, ry = .119 * k + off, .152 * k + off
        return Vector((math.sin(th) * rx, .02 + math.cos(th) * (ry - .02 + .01), z))
    # The brow, a chin bar and the lens cover: hinged top, a dark frame, the cyan lens.
    pc = Piece('mask-trim')
    tube(pc, catmull([face(a, .2, .007) for a in [math.radians(x) for x in range(-78, 79, 13)]], 2), .0085, 'iron-edge', segs=6)
    tube(pc, catmull([face(a, -.03, .007) for a in [math.radians(x) for x in range(-70, 71, 14)]], 2), .0072, 'iron-edge', segs=6)
    # a gusseted nose ridge under the lens
    box(pc, face(0, .068, .01), (.018, .014, .08), 'iron-edge', rot=Matrix.Rotation(math.radians(-5), 3, 'X'))
    # breathing slots either side of the ridge
    for sx in (-1, 1):
        for i in range(3):
            th = sx * math.radians(19 + i * 9)
            box(pc, face(th, .03 + i * .004, .005), (.012, .007, .05), 'grille', rot=Matrix.Rotation(-th, 3, 'Z'))
    out.append(pc.bevel(.0012, 1, 50).make())
    pc = Piece('mask-cover')
    lc = face(0, .132, .012)
    box(pc, lc, (.168, .018, .062), 'lens-rim')
    for z, h in ((.03, .012), (-.03, .012)):
        box(pc, lc + Vector((0, .006, z)), (.178, .014, h), 'iron-edge')
    for sx in (-1, 1):
        box(pc, lc + Vector((sx * .085, .006, 0)), (.012, .014, .064), 'iron-edge')
    cyl(pc, lc + Vector((-.06, .009, .038)), lc + Vector((-.03, .009, .038)), .0052, 'steel', segs=8)
    cyl(pc, lc + Vector((.03, .009, .038)), lc + Vector((.06, .009, .038)), .0052, 'steel', segs=8)
    out.append(pc.bevel(.0014, 1, 50).make())
    pc = Piece('mask-lens')
    box(pc, lc + Vector((0, .0095, 0)), (.148, .002, .042), 'lens-cyan')
    out.append(pc.make())
    pc = Piece('mask-rivets')
    rv = [face(math.radians(a), z, .0065) for a, z in ((-58, .185), (58, .185), (-72, .12), (72, .12), (-72, .04), (72, .04), (-58, -.02), (58, -.02), (-28, -.024), (28, -.024), (0, .2))]
    for p in rv:
        d = Vector((p.x, p.y - .02, 0)).normalized()
        cyl(pc, p - d * .001, p + d * .0034, .0055, 'rivet', segs=8, r1=.0035)
    out.append(pc.make())
    # The head straps: round the skull at the temple, over the crown, a buckle behind; spatter on the plate.
    pc = Piece('mask-straps')
    back = [Vector((math.sin(t) * .108, .012 + math.cos(t) * .118, .13)) for t in [math.radians(a) for a in range(82, 279, 14)]]
    ribbon(pc, back, [Vector((p.x, p.y - .012, .0)) for p in back], .03, .004, 'leather-dark')
    over = [Vector((0, .05 + .118 * math.cos(t), .1 + .128 * math.sin(t))) for t in [math.radians(a) for a in range(60, 181, 13)]]
    ribbon(pc, over, [Vector((0, p.y - .03, p.z - .1)) for p in over], .026, .004, 'leather-dark')
    box(pc, (0, -.111, .13), (.04, .008, .034), 'buckle')
    box(pc, (0, -.1155, .13), (.03, .004, .022), 'leather-dark')
    out.append(pc.make())
    return out


# ===========================================================================================================================
def ladle_hood():
    """The welder's leather cowl under the mask: a sewn cap over the skull, flaps over the ears, a curtain down the back of the neck."""
    out = []
    pc = Piece('hood')
    c, rx, ry, rz = Vector((0, .018, .1)), .114, .13, .13
    n, m = 30, 11
    rows = []
    for j in range(m + 1):
        v = j / m
        ph = math.radians(-58 + v * 145)
        row = []
        for i in range(n):
            t = 2 * math.pi * i / n
            cp = math.cos(ph)
            p = c + Vector((math.sin(t) * rx * cp, math.cos(t) * ry * cp, math.sin(ph) * rz))
            # stops short of the face's opening in front (the mask covers it): pulled back under the mask's edge
            front = max(0.0, math.cos(t))
            p.y -= .03 * front * front
            row.append(pc.bm.verts.new(p))
        rows.append(row)
    faces = []
    for j in range(m):
        for i in range(n):
            faces.append(pc.bm.faces.new((rows[j][i], rows[j][(i + 1) % n], rows[j + 1][(i + 1) % n], rows[j + 1][i])))
    pc.tag(faces, 'leather')
    for f in faces:
        f.normal_update()
        if f.normal.dot(f.calc_center_median() - c) < 0:
            f.normal_flip()
    out.append(pc.solidify(.005, 1.0).make(recalc=False))
    # The curtain: stiff leather from the cap's back edge to the shoulders, with its burn marks; the seams up the crown.
    pc = Piece('hood-curtain')
    a, b = [], []
    for i in range(15):
        t = math.radians(100 + i * 160 / 14)
        a.append(Vector((math.sin(t) * .098, .018 + math.cos(t) * .112, -.002)))
        b.append(Vector((math.sin(t) * .125, .018 + math.cos(t) * .135, -.105)))
    va, vb = [pc.bm.verts.new(p) for p in a], [pc.bm.verts.new(p) for p in b]
    faces = [pc.bm.faces.new((va[i], va[i + 1], vb[i + 1], vb[i])) for i in range(14)]
    pc.tag(faces, 'leather-dark')
    for f in faces:
        f.normal_update()
        if f.normal.dot(f.calc_center_median() - Vector((0, .018, -.05))) < 0:
            f.normal_flip()
    out.append(pc.solidify(.004, 1.0).make(recalc=False))
    pc = Piece('hood-seams')
    for x0 in (-.045, .045):
        k = math.sqrt(1 - (x0 / (rx + .005)) ** 2)
        seam = [Vector((x0, c.y + (ry + .006) * k * math.cos(p) - .03 * max(0.0, math.cos(p)) ** 2, c.z + (rz + .006) * k * math.sin(p))) for p in [math.radians(a2) for a2 in range(40, 186, 12)]]
        tube(pc, seam, .0026, 'leather-dark', segs=4)
    out.append(pc.make())
    return out


def rigger_hat(colour):
    """The hard hat (station_gear.py's shell, its lens dull: the lit lens is a prop drawn while its lamp is on, Runtime IronworksLampOnModel) in dirty cream."""
    import station_gear
    objs = station_gear.hardhat(colour)
    return objs


def forearm_cuff(side, r0, r1, x0, x1, fin, band, rivet_n=6, flare=.008):
    """A gauntlet's cuff on a forearm bone (its x along the forearm toward the hand; the left's x runs back toward the elbow in its frame: `side` -1):
    a tapered shell flaring at the elbow end, a rolled edge, two straps and rivets; the bone's own axes, no design frame."""
    out = []
    pc = Piece('cuff')
    n = 20
    def ring(x, r):
        return [Vector((side * x, r * math.cos(2 * math.pi * i / n), r * math.sin(2 * math.pi * i / n))) for i in range(n)]
    xs = [x0, x0 + .02, (x0 + x1) * .5, x1 - .012, x1]
    rs = [r0 + flare, r0 + flare * .4, (r0 + r1) * .5, r1, r1 - .003]
    rings = [[pc.bm.verts.new(p) for p in ring(x, r)] for x, r in zip(xs, rs)]
    faces = []
    for k in range(len(rings) - 1):
        for i in range(n):
            faces.append(pc.bm.faces.new((rings[k][i], rings[k][(i + 1) % n], rings[k + 1][(i + 1) % n], rings[k + 1][i])))
    pc.tag(faces, fin)
    for f in faces:
        f.normal_update()
        q = f.calc_center_median()
        if f.normal.dot(Vector((0, q.y, q.z))) < 0:
            f.normal_flip()
    out.append(pc.solidify(.004, 1.0).make(recalc=False))
    pc = Piece('cuff-trim')
    for x, r in ((x0 + .003, r0 + flare + .004), (x1 - .004, r1 + .003)):
        tube(pc, ring(x, r), .0042, band, segs=5, closed=True)
    for x, r in ((x0 + .05, r0 + flare * .3), (x1 - .05, r1 + .002)):
        tube(pc, ring(x, r + .004), .0052, 'strap', segs=4, closed=True)
        box(pc, (side * x, 0, r + .009), (.02, .03, .006), 'buckle')
    rivets(pc, [Vector((side * ((x0 + x1) * .5), (r0 + r1) * .5 * math.cos(t) * 1.03, (r0 + r1) * .5 * math.sin(t) * 1.03)) for t in [2 * math.pi * i / rivet_n for i in range(rivet_n)]], (0, 0, 1), .0035, .003)
    out.append(pc.make())
    return out


# ===========================================================================================================================
# Fitted surfaces: the worn bodies' outermost radii (ironworks_fits.py, measured from zdumps), with their normals
# ===========================================================================================================================
class Torso:
    """spine_05's design frame: a surface point at angle `a` (degrees, 0 front, + toward the right) and height `z`."""

    def __init__(self, key):
        t = FITS[key]['torso']
        self.key = key
        self.zs = sorted(t)
        self.rows = [list(t[z]) for z in self.zs]
        self.at_ = FITS[key]['at']
        for i, row in enumerate(self.rows):
            have = [k for k, v in enumerate(row) if v > 0]
            if not have:
                self.rows[i] = None
                continue
            for k, v in enumerate(row):
                if v <= 0:
                    near = min(have, key=lambda h: min((h - k) % 18, (k - h) % 18))
                    row[k] = row[near]
        keep = [(z, r) for z, r in zip(self.zs, self.rows) if r is not None]
        self.zs, self.rows = [z for z, _ in keep], [r for _, r in keep]

    def r(self, a, z):
        a = a % 360
        i = a / 20
        i0, f = int(i) % 18, i - int(i)
        i1 = (i0 + 1) % 18
        z = max(self.zs[0], min(self.zs[-1], z))
        k = 0
        while k < len(self.zs) - 2 and self.zs[k + 1] < z:
            k += 1
        g = (z - self.zs[k]) / max(1e-6, self.zs[k + 1] - self.zs[k])
        g = max(0.0, min(1.0, g))
        lo = self.rows[k][i0] * (1 - f) + self.rows[k][i1] * f
        hi = self.rows[k + 1][i0] * (1 - f) + self.rows[k + 1][i1] * f
        return lo * (1 - g) + hi * g

    def raw(self, a, z, off=0.0):
        t = math.radians(a)
        rr = self.r(a, z) + off
        return Vector((math.sin(t) * rr, math.cos(t) * rr, z))

    def __call__(self, a, z):
        p = self.raw(a, z)
        du = self.raw(a + 7, z) - self.raw(a - 7, z)
        dv = self.raw(a, z + .014) - self.raw(a, z - .014)
        n = du.cross(dv)
        if n.dot(Vector((p.x, p.y, 0))) < 0:
            n = -n
        return p, n.normalized()


class Limb:
    """A limb bone's own frame: a surface point at angle `a` about its x (degrees, 0 toward +y, + toward +z) and along `x`."""

    def __init__(self, key, bone):
        t = FITS[key][bone]
        self.xs = sorted(t)
        self.rows = [list(t[x]) for x in self.xs]
        for row in self.rows:
            have = [k for k, v in enumerate(row) if v > 0]
            for k, v in enumerate(row):
                if v <= 0 and have:
                    near = min(have, key=lambda h: min((h - k) % 12, (k - h) % 12))
                    row[k] = row[near]

    def r(self, a, x):
        a = a % 360
        i = a / 30
        i0, f = int(i) % 12, i - int(i)
        i1 = (i0 + 1) % 12
        x = max(self.xs[0], min(self.xs[-1], x))
        k = 0
        while k < len(self.xs) - 2 and self.xs[k + 1] < x:
            k += 1
        g = max(0.0, min(1.0, (x - self.xs[k]) / max(1e-6, self.xs[k + 1] - self.xs[k])))
        lo = self.rows[k][i0] * (1 - f) + self.rows[k][i1] * f
        hi = self.rows[k + 1][i0] * (1 - f) + self.rows[k + 1][i1] * f
        return lo * (1 - g) + hi * g

    def raw(self, a, x, off=0.0):
        t = math.radians(a)
        rr = self.r(a, x) + off
        return Vector((x, math.cos(t) * rr, math.sin(t) * rr))

    def __call__(self, a, x):
        p = self.raw(a, x)
        du = self.raw(a + 9, x) - self.raw(a - 9, x)
        dv = self.raw(a, x + .02) - self.raw(a, x - .02)
        n = du.cross(dv)
        if n.dot(Vector((0, p.y, p.z))) < 0:
            n = -n
        return p, n.normalized()


def patch(pc, surf, cu, cv, du, dv, thick, fin, seed, n=6, edge_fin=None, sink=.002):
    """A plate grown on a fitted surface about (cu, cv) (half sizes du, dv in its coordinates): a rounded outline roughened by noise, domed to `thick`, facetted."""
    off = Vector((seed * 2.1, seed * 3.3, seed * 1.3))
    grid = {}

    def vert(i, j):
        if (i, j) in grid:
            return grid[i, j]
        s, t = -1 + 2 * i / n, -1 + 2 * j / n
        sx, sy = s * math.sqrt(max(0.0, 1 - t * t * .5)), t * math.sqrt(max(0.0, 1 - s * s * .5))
        d = math.hypot(sx, sy)
        ang = math.atan2(sy, sx)
        rad = 1 + .24 * noise.noise(Vector((math.cos(ang), math.sin(ang), 0)) * 1.8 + off)
        u, v = cu + du * sx * rad, cv + dv * sy * rad
        p, nr = surf(u, v)
        h = thick * math.sqrt(max(0.0, 1 - d ** 3)) * (1 + .4 * noise.noise(p * 38.0 + off)) - sink
        grid[i, j] = pc.bm.verts.new(p + nr * h)
        return grid[i, j]
    faces = []
    for i in range(n):
        for j in range(n):
            a, b, c, e = vert(i, j), vert(i + 1, j), vert(i + 1, j + 1), vert(i, j + 1)
            if (i + j) % 2:
                faces.append(pc.bm.faces.new((a, b, c)))
                faces.append(pc.bm.faces.new((a, c, e)))
            else:
                faces.append(pc.bm.faces.new((a, b, e)))
                faces.append(pc.bm.faces.new((b, c, e)))
    p0, n0 = surf(cu, cv)
    k = 0
    for f in faces:
        f.normal_update()
        ref = f.calc_center_median() - p0 + n0 * .05
        if f.normal.dot(ref) < 0:
            f.normal_flip()
        k += 1
        f.material_index = pc.slot(edge_fin if edge_fin and (k * 7 + int(seed * 13)) % 5 == 0 else fin)
        f.tag = True
    return faces


def plates(pc, seams, surf, table, fin='slag', edge='slag-edge', crack='slag-crack', seam_scale=1.06):
    """Crust plates (cu, cv, du, dv, thick) with a glowing seam under each."""
    for i, (cu, cv, du, dv, th) in enumerate(table):
        seed = cu * .013 + cv * 7.1 + i * 1.37
        patch(pc, surf, cu, cv, du, dv, th, fin, seed, edge_fin=edge)
        if seams is not None:
            patch(seams, surf, cu, cv, du * seam_scale, dv * seam_scale, .0035, crack, seed, n=4, sink=.0)


# ===========================================================================================================================
# Slagback: the crust. Lumpy and uneven: heavy on the chest and one shoulder, plates grown onto the bones; the dim seams between them, the fissure down the back.
# The fissure stays when the crust sheds (its gear is apart: Runtime IronworksCrustGear).
# ===========================================================================================================================
def _slag_build(key, table):
    surf = Torso(key)
    pc = Piece('slag')
    seams = Piece('slag-seams')
    plates(pc, seams, surf, table)
    pc.sharp = 26
    return [pc.make(recalc=False), seams.make(recalc=False)]


def slag_chest(key):
    #        angle  z      da   dz   thick
    return _slag_build(key, ((-42, -.055, 30, .065, .036), (40, -.04, 28, .06, .03), (2, -.115, 22, .05, .022), (-22, -.2, 30, .055, .03), (24, -.205, 26, .05, .026),
                             (-64, -.14, 17, .07, .024), (64, -.13, 16, .07, .022), (0, -.3, 36, .04, .024), (6, .012, 22, .028, .016)))


def slag_back(key):
    return _slag_build(key, ((180 - 38, -.02, 24, .07, .034), (180 + 38, -.008, 24, .065, .036), (180 - 42, -.14, 22, .055, .028), (180 + 40, -.13, 22, .06, .03),
                             (180 - 40, -.25, 24, .045, .022), (180 + 44, -.25, 22, .05, .024), (180 - 74, -.08, 15, .08, .022), (180 + 74, -.09, 15, .075, .022)))


def slag_fissure(key):
    surf = Torso(key)
    pc = Piece('fissure')
    zs = [.025, 0, -.03, -.06, -.09, -.12, -.15, -.18, -.215, -.25, -.285, -.315]
    jag = [0, 2.5, -3, 2.2, -2, 3, -2.5, 2.2, -3, 2.5, -2, 0]
    mid = [surf.raw(180 + j, z, .004) for j, z in zip(jag, zs)]
    tube(pc, catmull(mid, 2), .009, 'fissure', segs=5, radii=[.004 + .009 * math.sin(math.pi * i / (2 * (len(zs) - 1))) for i in range(2 * (len(zs) - 1) + 1)])
    for k in ((1, 4, 8), (2, -5, 7)):
        i, da, l = k
        branch = [surf.raw(180 + jag[i], zs[i], .004), surf.raw(180 + jag[i] + da * 2, zs[i] - .025, .004), surf.raw(180 + jag[i] + da * 3.4, zs[i] - .05, .004)]
        tube(pc, catmull(branch, 2), .0035, 'fissure-rim', segs=4)
    out = [pc.make()]
    pc = Piece('fissure-lips')
    for side in (-1, 1):
        lip = [surf.raw(180 + j + side * 5.2, z, .007) for j, z in zip(jag, zs)]
        tube(pc, catmull(lip, 2), .0075, 'slag-edge', segs=5)
    out.append(pc.make())
    return out


def slag_head():
    pc = Piece('slag-head')
    seams = Piece('slag-head-seams')
    for th, ph, rx, ry, seed in ((-.35, 1.0, .06, .055, 1.3), (.8, .75, .055, .05, 2.2), (2.9, .95, .07, .06, 3.1), (-1.5, .62, .045, .055, 4.4), (1.9, .55, .05, .05, 5.7), (-2.2, .4, .05, .045, 6.2)):
        d = Vector((math.sin(th) * math.cos(ph), math.cos(th) * math.cos(ph), math.sin(ph)))
        at, n = onskull(SKULL_C + d, .006)
        rock(pc, at, (rx, ry, .022), along(n), 'slag', seed, sub=2, lump=.3, flat=.5)
        rock(seams, at - n * .012, (rx * 1.06, ry * 1.06, .008), along(n), 'slag-crack', seed, sub=1, lump=.12)
    pc.sharp = 28
    return [pc.make(), seams.make()]


def _limb_plates(key, bone, table):
    surf = Limb(key, bone)
    pc = Piece('slag-limb')
    seams = Piece('slag-limb-seams')
    plates(pc, seams, surf, table)
    pc.sharp = 26
    return [pc.make(recalc=False), seams.make(recalc=False)]


def _along(key, bone, f):
    """A share `f` along a limb from its near end (its bone's origin) out: its x (the left's run negative)."""
    xs = sorted(FITS[key][bone])
    far = xs[0] if abs(xs[0]) > abs(xs[-1]) else xs[-1]
    return far * f


def slag_limb(key, bone, spec):
    return _limb_plates(key, bone, [(a, _along(key, bone, f), da, abs(_along(key, bone, df)), th) for a, f, da, df, th in spec])


class Hips:
    """The pelvis bone's design frame (its axes as spine_05's): an ellipse from the torso's lowest measured slices, a point at angle `a` and height `z`."""

    def __init__(self, key):
        self.t = Torso(key)

    def r(self, a, z):
        return self.t.r(a, -.335) * (1 + .1 * max(0.0, -z) / .1)

    def raw(self, a, z, off=0.0):
        t = math.radians(a)
        rr = self.r(a, z) + off
        return Vector((math.sin(t) * rr, math.cos(t) * rr, z))

    def __call__(self, a, z):
        p = self.raw(a, z)
        return p, Vector((p.x, p.y, 0)).normalized()


def slag_pelvis(key):
    surf = Hips(key)
    pc = Piece('slag-hips')
    seams = Piece('slag-hips-seams')
    plates(pc, seams, surf, ((-55, .03, 30, .05, .024), (40, .045, 26, .04, .02), (180, .04, 36, .05, .022), (100, .0, 18, .05, .018)))
    pc.sharp = 26
    return [pc.make(recalc=False), seams.make(recalc=False)]


def cinder_rags(key='cinderling-m'):
    """What is left of an apron and a tool belt (pelvis): burned strips off a charred belt, short enough to clear the thighs, their ends still smouldering."""
    surf = Hips(key)
    out = []
    pc = Piece('rags')
    belt = [surf.raw(a, .055, .006) for a in range(0, 360, 20)]
    ribbon(pc, belt + belt[:1], [Vector((p.x, p.y, 0)) for p in belt + belt[:1]], .034, .005, 'leather-burnt')
    ends = []
    for i, (a, w, l) in enumerate(((-16, .07, .2), (14, .06, .16), (-46, .05, .12), (48, .05, .13), (180, .09, .2), (152, .05, .14), (212, .05, .11), (94, .04, .1), (-90, .04, .09))):
        pts = []
        for k in range(6):
            f = k / 5
            q = surf.raw(a + 3 * math.sin(i + f * 3), .05 - l * f, .009 + .012 * f)
            pts.append(q + Vector((.004 * math.sin(k * 2.1 + i), 0, 0)))
        widths = [w * (1 - .25 * f) * (1 + .22 * math.sin(k * 3.3 + i * 1.7)) for k, f in enumerate(k / 5 for k in range(6))]
        ribbon(pc, pts, [Vector((p.x, p.y, 0)) for p in pts], w, .0028, 'char-rag', widths=widths)
        ends.append((pts[-1], pts[-1] - pts[-2], w))
    out.append(pc.make())
    pc = Piece('rags-embers')
    for p, d, w in ends:
        n = Vector((p.x, p.y, 0)).normalized()
        s = n.cross(Vector((0, 0, 1)))
        tube(pc, [p + s * (w * .4), p + s * (w * .1) + Vector((0, 0, .004)), p - s * (w * .2) + Vector((0, 0, -.002)), p - s * (w * .42)], .0026, 'ember', segs=4)
    out.append(pc.make())
    return out


# ===========================================================================================================================
# The Cinderling's opened chest (spine_05): the burned ribs over a pit of char, the coal in it (its weak point; Core NativeZombieLooks.CinderCoal), flakes of char round the hole
# ===========================================================================================================================
CINDER_A, CINDER_Z = -24, -.085


def cinder_ribs(key):
    surf = Torso(key)
    out = []
    c, nc = surf(CINDER_A, CINDER_Z)
    pc = Piece('ribs-pit')
    patch(pc, surf, CINDER_A, CINDER_Z, 36, .082, .012, 'char-deep', 1.7, n=8, sink=-.001)
    # the burned rim of the hole, ragged
    rim = []
    for i in range(22):
        t = 2 * math.pi * i / 22
        rr = 1 + .14 * math.sin(t * 3 + 1) + .1 * math.sin(t * 7)
        rim.append(surf.raw(CINDER_A + math.cos(t) * 34 * rr, CINDER_Z + math.sin(t) * .08 * rr, .014 + .004 * math.sin(t * 5)))
    tube(pc, rim, .009, 'char', segs=5, closed=True, radii=[.008 + .0045 * abs(math.sin(i * 1.7)) for i in range(22)])
    out.append(pc.make())
    pc = Piece('ribs-coal')
    rock(pc, c + nc * .024, (.024, .028, .018), along(nc), 'coal-white', 3.3, sub=2, lump=.22)
    for i in range(7):
        t = i * .898 + .4
        q = surf.raw(CINDER_A + math.cos(t) * 13 * (1 + .25 * (i % 2)), CINDER_Z + math.sin(t) * .036 * (1 + .25 * (i % 2)), .014)
        rock(pc, q, (.019, .021, .014), along(nc), 'ember' if i % 2 else 'coal-hot', 4.1 + i, sub=1, lump=.2)
    out.append(pc.make())
    # the ribs: bone gone black, crossing the pit from the breastbone, their ends snapped and ember-tipped
    pc = Piece('ribs')
    tips = []
    for i, z in enumerate((-.035, -.065, -.098, -.132, -.162)):
        a0, a1 = 6, -60 + i * 2
        z0, z1 = z + .012, z - .012
        path = []
        for k in range(7):
            f = k / 6
            path.append(surf.raw(a0 + (a1 - a0) * f, z0 + (z1 - z0) * f, .018 + .018 * math.sin(math.pi * f)))
        snap = 6 - (i % 2)
        path = path[:snap + 1]
        tube(pc, catmull(path, 2), .0105, 'char-bone', segs=6, radii=None)
        tips.append(path[-1])
    # the breastbone, charred through
    tube(pc, catmull([surf.raw(6, -.015, .012), surf.raw(7, -.08, .019), surf.raw(6, -.14, .018), surf.raw(4, -.19, .012)], 3), .011, 'char-bone', segs=6)
    out.append(pc.make())
    pc = Piece('ribs-tips')
    for p in tips:
        sphere(pc, p, (.006, .006, .006), 'ember', u=6, v=4)
    out.append(pc.make())
    pc = Piece('ribs-flakes')
    for i, (a, z, da, dz, th) in enumerate(((35, -.06, 18, .04, .012), (46, -.16, 15, .05, .012), (-68, -.03, 13, .03, .012), (-66, -.21, 14, .04, .012), (10, -.25, 20, .03, .011), (150, -.08, 22, .05, .012), (-150, -.17, 22, .06, .012))):
        patch(pc, surf, a, z, da, dz, th, 'char', 7.1 + i, n=4, edge_fin='ash')
    pc.sharp = 30
    out.append(pc.make(recalc=False))
    return out


# ===========================================================================================================================
# The Stoker's cape and pack (spine_05), gauntlets (forearms)
# ===========================================================================================================================
def stoker_cape(key):
    surf = Torso(key)
    at = surf.at_
    sx = abs(at['upperarm_r'][0])
    top = at['neck_01'][2] + .01
    out = []
    pc = Piece('cape')
    # A skirt from the collar over the shoulders to the upper arm's top: rings of a superellipse, wide across, the chest's and the back's own depth.
    rings = []
    N = 40
    prof = ((top, .085, .095), (top - .02, .115, .105), (top - .05, sx * .82, .13), (top - .085, sx + .055, .155), (top - .125, sx + .07, .17), (top - .165, sx + .06, .175))
    for zv, rx, ry in prof:
        ring = []
        for i in range(N):
            t = 2 * math.pi * i / N
            sn, cs = math.sin(t), math.cos(t)
            k = (abs(sn) ** 3 + abs(cs) ** 3) ** (-1 / 3)
            y = cs * k * ry
            fr = surf.r(0 if cs > 0 else 180, zv) + .02
            if abs(cs) > .3:
                y = max(abs(y), fr * abs(cs) ** .5) * (1 if cs > 0 else -1)
            ring.append(pc.bm.verts.new(Vector((sn * k * rx, y + (.005 if cs > 0 else -.005), zv))))
        rings.append(ring)
    faces = []
    for a in range(len(rings) - 1):
        for i in range(N):
            faces.append(pc.bm.faces.new((rings[a][i], rings[a][(i + 1) % N], rings[a + 1][(i + 1) % N], rings[a + 1][i])))
    pc.tag(faces, 'foil')
    for f in faces:
        f.normal_update()
        q = f.calc_center_median()
        if f.normal.dot(Vector((q.x, q.y, 0))) < 0:
            f.normal_flip()
    out.append(pc.solidify(.006, 1.0).bevel(.002, 1, 40).make(recalc=False))
    # Quilt lines round it, seams from the collar out, a chest strap and buckle.
    pc = Piece('cape-quilt')
    for a in (1, 2, 3, 4):
        zv, rx, ry = prof[a]
        loop = []
        for i in range(N):
            t = 2 * math.pi * i / N
            v = rings[a][i].co if False else None
        k = a
    for i in range(0, N, 4):
        path = []
        for zv, rx, ry in prof[1:]:
            t = 2 * math.pi * i / N
            sn, cs = math.sin(t), math.cos(t)
            kk = (abs(sn) ** 3 + abs(cs) ** 3) ** (-1 / 3)
            y = cs * kk * ry
            fr = surf.r(0 if cs > 0 else 180, zv) + .02
            if abs(cs) > .3:
                y = max(abs(y), fr * abs(cs) ** .5) * (1 if cs > 0 else -1)
            o = Vector((sn, cs, 0))
            path.append(Vector((sn * kk * rx, y + (.005 if cs > 0 else -.005), zv)) + o * .0075)
        tube(pc, catmull(path, 2), .0035, 'quilt', segs=4)
    fz = top - .15
    fy = surf.r(0, fz) + .032
    box(pc, (0, fy, fz), (.06, .012, .045), 'buckle')
    box(pc, (0, fy + .006, fz), (.038, .006, .028), 'strap')
    for sx2 in (-1, 1):
        box(pc, (sx2 * .1, fy - .002, fz), (.14, .006, .03), 'strap')
    out.append(pc.make())
    return out


def stoker_pack(key):
    surf = Torso(key)
    out = []
    yb = -surf.r(180, -.12) - .012
    pc = Piece('pack-plate')
    box(pc, (0, yb - .012, -.13), (.25, .024, .34), 'canister')
    for sx in (-1, 1):
        box(pc, (sx * .1, yb - .001, -.13), (.05, .012, .3), 'strap')
    out.append(pc.bevel(.005, 2, 45).make())
    pc = Piece('pack-tanks')
    for sx, h in ((-.062, .3), (.062, .26)):
        cyl(pc, (sx, yb - .075, -.13 - h / 2), (sx, yb - .075, -.13 + h / 2), .052, 'canister', segs=16)
        sphere(pc, (sx, yb - .075, -.13 + h / 2), (.052, .052, .03), 'canister', u=16, v=7)
        sphere(pc, (sx, yb - .075, -.13 - h / 2), (.052, .052, .026), 'canister', u=16, v=7)
        torus(pc, (sx, yb - .075, -.13 + h / 2 - .04), (0, 0, 1), .054, .0045, 'iron-edge', segs=16, rsegs=4)
        torus(pc, (sx, yb - .075, -.13 - h / 2 + .04), (0, 0, 1), .054, .0045, 'iron-edge', segs=16, rsegs=4)
    # hazard band
    out.append(pc.bevel(.0015, 1, 50).make())
    pc = Piece('pack-valves')
    v = Vector((0, yb - .075, .05))
    box(pc, v, (.11, .03, .026), 'iron-edge')
    cyl(pc, v + Vector((0, 0, .012)), v + Vector((0, 0, .04)), .008, 'steel', segs=8)
    torus(pc, v + Vector((0, 0, .044)), (0, 0, 1), .019, .004, 'hazard-y', segs=12, rsegs=4)
    # the gauge
    g = Vector((.09, yb - .1, .05))
    cyl(pc, g + Vector((0, .02, 0)), g, .022, 'iron-edge', segs=16)
    decal(pc, g + Vector((0, -.0006, 0)), (-1, 0, 0), (0, 0, 1), .034, .034, .0024, lambda a, b: None if math.hypot(a, b) > .5 else ('gauge-ink' if (math.hypot(a, b) > .42 or (abs(a + b * .4) < .05 and b > -.05)) else 'gauge'))
    out.append(pc.bevel(.001, 1, 50).make(recalc=False))
    # the hose out of the valve over the shoulder to the cape's collar
    pc = Piece('pack-hose')
    top = surf.at_['neck_01'][2]
    pts = [v + Vector((-.05, 0, 0)), v + Vector((-.1, .0, .018)), Vector((-.15, yb - .012, top - .005)), Vector((-.115, -.01, top + .012)), Vector((-.09, .09, top - .03))]
    path = catmull(pts, 5)
    tube(pc, path, .012, 'hose', segs=8)
    for i in range(2, len(path) - 1):
        torus(pc, path[i], path[i + 1] - path[i - 1], .0125, .0022, 'hose', segs=8, rsegs=3)
    out.append(pc.make())
    # a cyan status lamp
    pc = Piece('pack-led')
    box(pc, (-.1, yb - .026, .02), (.014, .006, .014), 'led-cyan')
    out.append(pc.make())
    return out


def cuff_fit(key, bone, fin, band, x0=.07, x1=.95, off=.007):
    """A gauntlet's cuff or a spat on a limb bone: a shell laid over the limb's fitted surface (its radii smoothed round it), flared at its near end, a rolled edge at each, two straps and buckles, rivets."""
    surf = Limb(key, bone)
    xs = sorted(FITS[key][bone])
    side = -1 if abs(xs[0]) > abs(xs[-1]) else 1
    xa, xb = abs(_along(key, bone, x0)), abs(_along(key, bone, x1))
    n = 20
    out = []

    def rad(a, x):
        return (surf.r(a - 30, x) + 2 * surf.r(a, x) + surf.r(a + 30, x)) / 4

    def ring(x, extra):
        q = []
        for i in range(n):
            a = i * 360 / n
            r = max(rad(a, side * x), rad(a, side * (x - .03)), rad(a, side * (x + .03))) * .96 + extra
            t = math.radians(a)
            q.append(Vector((side * x, math.cos(t) * r, math.sin(t) * r)))
        return q
    pc = Piece('cuff')
    steps = 7
    rings = []
    for k in range(steps):
        f = k / (steps - 1)
        x = xa + (xb - xa) * f
        rings.append([pc.bm.verts.new(p) for p in ring(x, off + .012 * (1 - f) ** 3)])
    faces = []
    for k in range(steps - 1):
        for i in range(n):
            faces.append(pc.bm.faces.new((rings[k][i], rings[k][(i + 1) % n], rings[k + 1][(i + 1) % n], rings[k + 1][i])))
    pc.tag(faces, fin)
    for f in faces:
        f.normal_update()
        q = f.calc_center_median()
        if f.normal.dot(Vector((0, q.y, q.z))) < 0:
            f.normal_flip()
    out.append(pc.solidify(.004, 1.0).make(recalc=False))
    pc = Piece('cuff-trim')
    tube(pc, ring(xa, off + .016), .0045, band, segs=5, closed=True)
    tube(pc, ring(xb, off + .004), .0045, band, segs=5, closed=True)
    for f in (.3, .72):
        x = xa + (xb - xa) * f
        loop = ring(x, off + .006)
        tube(pc, loop, .0055, 'strap', segs=4, closed=True)
        top = loop[5]
        box(pc, top + Vector((0, 0, .002)), (.02, .03, .007), 'buckle')
    x = (xa + xb) / 2
    loop = ring(x, off + .004)
    rivets(pc, [loop[i] for i in range(1, n, 4)], (0, 0, 1), .0035, .003)
    out.append(pc.make())
    return out


# ===========================================================================================================================
# The Rigger's harness and chain (spine_05): the shoulder straps down the front and back, a chest strap, a ring at the sternum; a chain coil worn as a bandolier
# ===========================================================================================================================
def _front_back(surf, x, zlo, ztop, off):
    """A strap's line at |X| = x: up the front from zlo to the shoulder's top at ztop, then down the back to zlo; points and normals."""
    def solve(front, z):
        lo, hi = (0.0, 89.0)
        for _ in range(16):
            m = (lo + hi) / 2
            if surf.r(m if front else 180 - m, z) * math.sin(math.radians(m)) < abs(x):
                lo = m
            else:
                hi = m
        a = (lo + hi) / 2
        a = a if front else 180 - a
        return a if x > 0 else -a
    pts, nrm = [], []
    z = zlo
    st = .022
    while z < ztop - .001:
        p, n = surf(solve(True, z), z)
        pts.append(p + n * off)
        nrm.append(n)
        z += st
    nf = len(pts)
    tailf, tailb = None, None
    bk = []
    z = zlo
    while z < ztop - .001:
        p, n = surf(solve(False, z), z)
        bk.append((p + n * off, n))
        z += st
    # over the top: an arc across from the front's last to the back's last
    a, b = pts[-1], bk[-1][0]
    apex = Vector((x, (a.y + b.y) / 2, max(a.z, b.z) + .018))
    pts += [a.lerp(apex, .6) + Vector((0, 0, .004)), apex, b.lerp(apex, .6) + Vector((0, 0, .004))]
    nrm += [Vector((0, .5, 1)), Vector((0, 0, 1)), Vector((0, -.5, 1))]
    for q, n in reversed(bk):
        pts.append(q)
        nrm.append(n)
    return pts, nrm


def rigger_harness(key):
    surf = Torso(key)
    at = surf.at_
    top = max(at['clavicle_r'][2], at['upperarm_r'][2]) + .02
    out = []
    pc = Piece('harness')
    for side in (-1, 1):
        pts, nrm = _front_back(surf, side * .082, -.27, top - .02, .004)
        ribbon(pc, catmull(pts, 1), catmull(nrm, 1), .042, .005, 'webbing')
    # the chest strap, the ring at the sternum, a back cross
    zc = -.12
    chest = [surf.raw(a, zc, .009) for a in range(-34, 35, 8)]
    ribbon(pc, chest, [Vector((p.x, p.y, 0)) for p in chest], .03, .004, 'webbing-edge')
    back = []
    for t in range(0, 11):
        f = t / 10
        back.append((surf.raw(180 - 30 + 60 * f, -.05 - .16 * f, .009), surf.raw(180 + 30 - 60 * f, -.05 - .16 * f, .011)))
    ribbon(pc, [b[0] for b in back], [Vector((b[0].x, b[0].y, 0)) for b in back], .036, .004, 'webbing-edge')
    ribbon(pc, [b[1] for b in back], [Vector((b[1].x, b[1].y, 0)) for b in back], .036, .004, 'webbing-edge')
    out.append(pc.make(recalc=False))
    pc = Piece('harness-steel')
    p, n = surf(0, zc)
    box(pc, p + n * .016, (.05, .01, .046), 'buckle')
    torus(pc, p + n * .03 + Vector((0, 0, -.02)), (0, 1, 0), .022, .0052, 'steel-worn', segs=14, rsegs=5)
    for side in (-1, 1):
        q, nq = surf(side * 18, -.03)
        box(pc, q + nq * .012, (.055, .009, .022), 'buckle')
        q, nq = surf(side * 20, -.24)
        torus(pc, q + nq * .018, (0, 1, 0), .017, .0042, 'steel-worn', segs=12, rsegs=5)
    q, nq = surf(180, -.13)
    torus(pc, q + nq * .026, (0, 1, 0), .03, .0065, 'steel-worn', segs=16, rsegs=5)
    out.append(pc.make())
    return out


def rigger_coil(key):
    surf = Torso(key)
    pc = Piece('coil')
    # A bandolier of chain from the left shoulder (its -X) to the right hip, laid on the torso, link by link.
    zm, k = -.14, .135
    path = []
    for i in range(121):
        a = i * 3
        z = zm + k * math.cos(math.radians(a + 60))
        path.append(surf.raw(a, z, .013))
    links = 0
    d, i = 0.0, 0
    step = .028
    total = 0.0
    seg = [0.0]
    for j in range(len(path) - 1):
        total += (path[j + 1] - path[j]).length
        seg.append(total)
    q = 0.0
    while q < total - .01:
        while i < len(seg) - 2 and seg[i + 1] < q:
            i += 1
        f = (q - seg[i]) / max(1e-6, seg[i + 1] - seg[i])
        p = path[i].lerp(path[i + 1], f)
        t = (path[i + 1] - path[i]).normalized()
        out_n = Vector((p.x, p.y, 0)).normalized()
        axis = (out_n - t * out_n.dot(t)).normalized() if links % 2 == 0 else t.cross(out_n).normalized()
        u = axis.cross(t).normalized()
        ring = [p + t * (.018 * math.cos(w)) + u * (.0105 * math.sin(w)) for w in [2 * math.pi * m / 8 for m in range(8)]]
        tube(pc, ring, .0037, 'steel', segs=4, closed=True)
        q += step
        links += 1
    # A shackle hanging at the hip end.
    p = surf.raw(150, zm + k * math.cos(math.radians(210)), .02)
    torus(pc, p + Vector((0, 0, -.03)), (1, 0, 0), .026, .0058, 'steel-worn', segs=14, rsegs=5)
    return [pc.make()]


def _props():
    fa = Matrix.Identity(4)
    props = [
        ('cinder-skull', 'head', HEAD, cinder_skull),
        ('cinder-cap', 'head', HEAD, cinder_cap),
        ('stoker-hood', 'head', HEAD, lambda: hood(False)),
        ('stoker-hood-dome', 'head', HEAD, lambda: hood(True)),
        ('stoker-rake', 'prop', PROP, rake),
        ('ladle-mask', 'head', HEAD, ladle_mask),
        ('ladle-hood', 'head', HEAD, ladle_hood),
        ('ladle-full', 'prop', PROP, lambda: ladle(True)),
        ('ladle-empty', 'prop', PROP, lambda: ladle(False)),
        ('rigger-hook', 'prop', PROP, hook),
        ('rigger-lamp-lit', 'prop', PROP, lamp_lit),
        ('slag-head', 'head', HEAD, slag_head),
        ('rigger-hat', 'head', HEAD, lambda: rigger_hat('hat-cream')),
        ('rigger-hat-worn', 'head', HEAD, lambda: rigger_hat('hat-cream-worn')),
    ]
    if not FITS:
        return props
    m, f = 'slagback-m', 'slagback-f'
    props += [
        ('slag-chest', 'spine_05', CHEST, lambda: slag_chest(m)), ('slag-chest-f', 'spine_05', CHEST, lambda: slag_chest(f)),
        ('slag-back', 'spine_05', CHEST, lambda: slag_back(m)), ('slag-back-f', 'spine_05', CHEST, lambda: slag_back(f)),
        ('slag-fissure', 'spine_05', CHEST, lambda: slag_fissure(m)), ('slag-fissure-f', 'spine_05', CHEST, lambda: slag_fissure(f)),
        ('cinder-ribs', 'spine_05', CHEST, lambda: cinder_ribs('cinderling-m')), ('cinder-ribs-f', 'spine_05', CHEST, lambda: cinder_ribs('cinderling-f')),
        ('stoker-cape', 'spine_05', CHEST, lambda: stoker_cape('stoker-m')), ('stoker-cape-f', 'spine_05', CHEST, lambda: stoker_cape('stoker-f')),
        ('stoker-pack', 'spine_05', CHEST, lambda: stoker_pack('stoker-m')), ('stoker-pack-f', 'spine_05', CHEST, lambda: stoker_pack('stoker-f')),
        ('stoker-gauntlet-l', 'lowerarm_l', fa, lambda: cuff_fit('stoker-m', 'lowerarm_l', 'foil', 'foil-dark', .3, .95, .003)),
        ('stoker-gauntlet-r', 'lowerarm_r', fa, lambda: cuff_fit('stoker-m', 'lowerarm_r', 'foil', 'foil-dark', .3, .95, .003)),
        ('ladle-gauntlet-l', 'lowerarm_l', fa, lambda: cuff_fit('ladleman-m', 'lowerarm_l', 'leather', 'leather-dark', .3, .96, .003)),
        ('ladle-gauntlet-r', 'lowerarm_r', fa, lambda: cuff_fit('ladleman-m', 'lowerarm_r', 'leather', 'leather-dark', .3, .96, .003)),
        ('rigger-harness', 'spine_05', CHEST, lambda: rigger_harness('rigger-m')), ('rigger-harness-f', 'spine_05', CHEST, lambda: rigger_harness('rigger-f')),
        ('rigger-coil', 'spine_05', CHEST, lambda: rigger_coil('rigger-m')), ('rigger-coil-f', 'spine_05', CHEST, lambda: rigger_coil('rigger-f')),
        ('cinder-rags', 'pelvis', CHEST, cinder_rags),
        ('slag-pelvis', 'pelvis', CHEST, lambda: slag_pelvis(m)),
        ('ladle-spat-l', 'calf_l', fa, lambda: cuff_fit('ladleman-m', 'calf_l', 'leather', 'leather-dark', .68, .95, .004)),
        ('ladle-spat-r', 'calf_r', fa, lambda: cuff_fit('ladleman-m', 'calf_r', 'leather', 'leather-dark', .68, .95, .004)),
    ]
    # the crust on the limbs: angle, share along, half angle, half share, thick (each side its own, asymmetric: one shoulder heavy)
    def shoulder(b, h):
        return lambda: slag_limb(m, b, ((70, .1, 62, .13, .05 * h), (250, .14, 60, .13, .036 * h), (340, .2, 40, .11, .026 * h)))

    def arm(b, h):
        return lambda: slag_limb(m, b, ((110, .55, 55, .2, .026 * h), (260, .62, 48, .17, .022), (20, .68, 44, .14, .018)))

    def forearm(b, h):
        return lambda: slag_limb(m, b, ((90, .33, 75, .18, .03 * h), (270, .42, 70, .2, .024), (0, .72, 62, .15, .022), (180, .78, 60, .14, .022)))

    def thigh(b, h):
        return lambda: slag_limb(m, b, ((0, .3, 62, .14, .026 * h), (70, .56, 40, .13, .02), (-60, .62, 38, .12, .02)))
    props += [
        ('slag-shoulder-l', 'upperarm_l', fa, shoulder('upperarm_l', 1.0)), ('slag-shoulder-r', 'upperarm_r', fa, shoulder('upperarm_r', .72)),
        ('slag-arm-l', 'upperarm_l', fa, arm('upperarm_l', 1.0)), ('slag-arm-r', 'upperarm_r', fa, arm('upperarm_r', .72)),
        ('slag-forearm-l', 'lowerarm_l', fa, forearm('lowerarm_l', 1.0)), ('slag-forearm-r', 'lowerarm_r', fa, forearm('lowerarm_r', .72)),
        ('slag-thigh-l', 'thigh_l', fa, thigh('thigh_l', 1.0)), ('slag-thigh-r', 'thigh_r', fa, thigh('thigh_r', .72)),
    ]
    return props


def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument('--out', required=True, help="a checkout's Assets/LocalAssets")
    ap.add_argument('--preview', help='render each prop here (workbench, its vertex colours)')
    ap.add_argument('--only', default='')
    ap.add_argument('--no-converter', action='store_true', help='the five kinds only (quicker while working on them)')
    a = ap.parse_args(argv)
    gk.clear_scene()
    import station_gear
    station_gear.finishes()
    finishes()
    gb = gk.GearBank()
    only = set(x for x in a.only.split(',') if x)
    for i, (prop, bone, frame, build) in enumerate(_props()):
        if only and prop not in only:
            continue
        objs = build()
        gm = gk.bake(prop, objs, frame, seed=i * 1.37 + 40)
        gb.add(prop, bone, gm)
        if a.preview:
            gk.preview(gm, frame, a.preview, prop)
        for ob in objs:
            bpy.data.objects.remove(ob, do_unlink=True)
    # THE CONVERTER, joint by joint, in its three detail tiers (converter_gear.py).
    cg.finishes()
    for j, (joint, build, kind) in enumerate(() if a.no_converter else cg.parts()):
        for tier, ratio in enumerate(cg.TIERS):
            prop = 'conv-%s-%d' % (joint, tier)
            if only and prop not in only and joint not in only:
                continue
            objs = build()
            if ratio < 1:
                for ob in objs:
                    # a small solid (a hub, a bolt ring) collapses into spikes: each object keeps at least its 70 faces
                    faces = len(ob.data.polygons)
                    keep = max(ratio, min(1.0, 70 / max(1, faces)))
                    if keep < 1:
                        m = ob.modifiers.new('decimate', 'DECIMATE')
                        m.ratio = keep
            gm = gk.bake(prop, objs, cg.FRAME, seed=j * 1.13 + 7)
            cg.grime(gm, kind)
            gb.add(prop, joint, gm)
            if a.preview and tier == 0:
                gk.preview(gm, cg.FRAME, a.preview, prop)
            for ob in objs:
                bpy.data.objects.remove(ob, do_unlink=True)
    for j, (prop, build) in enumerate(() if a.no_converter else cg.CHAIN):
        if only and prop not in only:
            continue
        objs = build()
        gm = gk.bake(prop, objs, cg.FRAME, seed=j * 1.9)
        gb.add(prop, 'prop', gm)
        for ob in objs:
            bpy.data.objects.remove(ob, do_unlink=True)
    gb.write(a.out, BANK)


if __name__ == '__main__':
    main()
