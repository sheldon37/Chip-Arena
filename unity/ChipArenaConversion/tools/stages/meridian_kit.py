"""Meridian's city kit (tools/stages/meridian_kit.py: board 3's approved city_kit, pass 2, ported unchanged but for its path to
stagekit; meridian_bank.py builds the game's bank from it): the pieces the City tileset's modules share (the MERIDIAN art bible, sections 2, 4, 5, 10 and 11).

Runs inside Blender 5.2 (bpy, numpy) on stagekit (tools/stages/stagekit.py): Geo faces with their own corners, finishes
(`fin`), the bake. Everything is in the game's space (Unity: metres, +y up), so these scripts can join tools/stages/ as
`city_kit.py` unchanged (they then import stagekit from beside themselves).

The module frame (bible 11.1): a facade's `Frame` has its origin on the facade plane (the collider face) at the module's
foot, u along the facade, v up and n = u x v out of it. A frame is never mirrored, so in Unity's left-handed space a reader in
the street sees u run from right to left (brickfall_kit's frames do the same); lettering is laid mirrored so it reads.

Tiers (bible 10.2): `CityTier(0)` is LOD0 (stagekit's Fine on leaner counts: arcs 12, rounds 10, small 6; vertex-colour cells
of .75 m to 6 m and 1.5 m above; bevels, detail and the tiniest parts on) and `CityTier(1)` is LOD1 (stagekit's Low: arcs 6,
rounds 6, small 4, 5 m cells, no bevels or detail). LOD1 keeps rooflines, deep relief, the window grid as lit and dark panes
and glowing letters.

Pass 2 (2026-11-19):
- Swept and tubular parts are drawn by this kit's own `sweep` and `tube`, never stagekit's: stagekit.sweep turns every face
  INTO its section, whichever way the section winds, so its ridges, copings, rails, arms and ribs come out inside out under
  the game's back-face culling (checks/inside_out.py measures it). stagekit itself is left as it is, for the older stages.
- Lathes are walked with the solid on the left (up the outside, inward over the top); a thin shell that is seen from both
  sides (a lamp's dish shade) is `lathe_shell`, two surfaces and a rim.
- `wall` merges each pier's cells up the face where nothing changes, so a face cut by many windows and bands costs far fewer
  triangles at the same vertex-colour cell size.
- Weathering grammar for the long night: ghost signs, pattress plates, boarded windows as real boards, rain stains.
"""
import math
import os
import random
import sys

sys.dont_write_bytecode = True  # never leave a __pycache__ in the repository's tools/stages
HERE = os.path.dirname(os.path.abspath(__file__))
STAGES = os.environ.get('MERIDIAN_STAGEKIT', HERE)
for _p in (HERE, STAGES):
    if _p not in sys.path:
        sys.path.insert(0, _p)

import stagekit as sk  # noqa: E402
from stagekit import Geo, _add, _cross, _dot, _norm, _sub  # noqa: E402

# ---------------------------------------------------------------------------------------------------------------------------
# The grid (bible 2.3)
# ---------------------------------------------------------------------------------------------------------------------------
BAY = 2.5          # m, four bays to a 10 m cell
GROUND = 4.0       # m, the ground storey
STOREY = 3.0       # m, every upper storey
PLINTH = .75       # m, the dark plinth
PROUD_LOW = .25    # m, art proud of a collider below 3.1 m
LOW_TOP = 3.1      # m, the height above which overhead art projects freely


def storey_floor(k):
    """The floor of storey k (0 the ground storey)."""
    return 0.0 if k <= 0 else GROUND + (k - 1) * STOREY


# ---------------------------------------------------------------------------------------------------------------------------
# Tiers
# ---------------------------------------------------------------------------------------------------------------------------
class CityTier:
    """LOD0 and LOD1 of the bible's policy, with the attributes stagekit's primitives read (i, cell, bevel, ...)."""

    def __init__(self, lod):
        self.lod = lod
        self.i = 0 if lod == 0 else 2          # stagekit's tier index: box() and obox() read it
        self.name = ('lod0', 'lod1')[lod]
        self.arc = (12, 6)[lod]
        self.round = (10, 6)[lod]
        self.small = (6, 4)[lod]
        self.cell = (.75, 5.0)[lod]            # the actor band (to 6 m) and the shopfronts
        self.cell_high = (1.5, 5.0)[lod]       # above 6 m
        self.bevel = (1, 0)[lod]
        self.fine = lod == 0
        self.detail = lod == 0
        self.tiny = lod == 0

    def cell_at(self, y):
        return self.cell if y < 6.0 else self.cell_high


# ---------------------------------------------------------------------------------------------------------------------------
# Finishes (bible 4.2): one table for the whole city
# ---------------------------------------------------------------------------------------------------------------------------
F = {}
GLOW = {}


def finishes():
    """The city's finishes by name in F (made once; stagekit keeps the table)."""
    if F:
        return F

    def add(name, *a, **k):
        F[name] = sk.fin('mer-' + name, *a, **k)
    B, FL, PL, NO, WD = sk.DETAIL_BRICK, sk.DETAIL_FLAG, sk.DETAIL_PLASTER, sk.DETAIL_NONE, sk.DETAIL_WOOD
    # masonry
    add('brick-stock', 0x8C4B36, B, smooth=.18, dirt=1.0, moss=.25, wear=.10, param=0.0)
    add('brick-blue', 0x4E4450, B, smooth=.32, dirt=.8, moss=.15, wear=.05, param=1.0)
    add('brick-buff', 0xB48A5E, B, smooth=.15, dirt=1.3, moss=.10, wear=.10, param=1.05)
    add('brick-glazed', 0xE3D8BC, B, smooth=.55, dirt=.6, wear=.05, param=1.3)
    add('brick-red', 0x9A4632, B, smooth=.16, dirt=1.0, moss=.2, wear=.1, param=0.0)    # the tenements' bright red
    add('render-cream', 0xD8CAA8, PL, smooth=.13, dirt=1.15, moss=.2, wear=.18)
    add('render-mint', 0xA6C2AE, PL, smooth=.13, dirt=1.15, moss=.2, wear=.18)
    add('render-rose', 0xC9A196, PL, smooth=.13, dirt=1.15, moss=.2, wear=.18)
    add('render-powder', 0xA2B4C4, PL, smooth=.13, dirt=1.15, moss=.2, wear=.18)
    add('render-ochre', 0xCDAA68, PL, smooth=.13, dirt=1.15, moss=.2, wear=.18)
    add('stone-dress', 0xCFC5AF, FL, smooth=.20, dirt=1.0, moss=.15, wear=.25, param=.6)
    add('stone-civic', 0xD6CDB8, FL, smooth=.22, dirt=.8, moss=.10, wear=.20, param=1.0)
    add('stone-rust', 0xC4B9A2, FL, smooth=.2, dirt=.9, moss=.12, wear=.2, param=1.0)   # rusticated courses
    add('granite', 0x7A7670, FL, smooth=.35, dirt=.8, moss=.1, wear=.3, param=.5)
    add('setts', 0x5A5652, FL, smooth=.45, dirt=.9, moss=.3, wear=.2, param=.22)
    add('paving', 0x6E6A64, FL, smooth=.40, dirt=.9, moss=.2, wear=.2, param=.62)
    add('asphalt', 0x2E3038, NO, smooth=.50, dirt=.5)
    add('puddle', 0x23252C, NO, smooth=.88, dirt=0.0)
    add('road-paint', 0xE6E0C8, NO, smooth=.3, dirt=.4, wear=.6)
    add('concrete', 0x8A8882, PL, smooth=.10, dirt=1.4, moss=.2, wear=.1)
    add('mortar-dark', 0x3A3436, NO, smooth=.1, dirt=.3)
    # roofs
    add('slate', 0x4A5262, NO, smooth=.50, dirt=.6, moss=.35, wear=.05)
    add('slate-dark', 0x3A404E, NO, smooth=.45, dirt=.6, moss=.3)
    add('lead', 0x6E767C, NO, smooth=.45, metal=.6, dirt=.5, wear=.1)
    add('copper', 0x5E9C88, sk.DETAIL_BRASS, smooth=.35, metal=.35, dirt=.4, wear=.2)
    add('tar-felt', 0x302E34, PL, smooth=.20, dirt=.5, moss=.3)
    add('terracotta', 0xA0583C, NO, smooth=.3, dirt=.9, wear=.2)                      # chimney pots, ridge tiles
    add('batten', 0x241E1E, NO, smooth=.1, dirt=.2)                                   # a missing slate's dark gap
    # metal
    add('iron-green', 0x24463A, NO, smooth=.40, metal=.25, dirt=.8, moss=.1, wear=.35)
    add('iron-black', 0x26262C, NO, smooth=.45, metal=.30, dirt=.7, wear=.30)
    add('iron-raw', 0x4A4E52, sk.DETAIL_IRON, smooth=.35, metal=.7, dirt=1.2, wear=.2)
    add('galv', 0x9AA0A4, NO, smooth=.50, metal=.8, dirt=.6, wear=.1)
    add('brass', 0xD2A840, sk.DETAIL_BRASS, smooth=.70, metal=1.0, dirt=.3, wear=.3)
    add('gilt', 0xE0B54A, NO, smooth=.75, metal=1.0, dirt=.15, wear=.2)
    # enamel and paint
    add('enamel-green', 0x1F6A4A, NO, smooth=.75, dirt=.3, wear=.2)
    add('enamel-cream', 0xEFE6CC, NO, smooth=.75, dirt=.3, wear=.2)
    add('paint-green', 0x2C5A48, WD, smooth=.45, dirt=.8, moss=.1, wear=.3, param=1.0)
    add('paint-oxblood', 0x6A2626, WD, smooth=.45, dirt=.8, moss=.1, wear=.3, param=1.0)
    add('paint-navy', 0x233150, WD, smooth=.45, dirt=.8, moss=.1, wear=.3, param=1.0)
    add('paint-cream', 0xE4D9BC, WD, smooth=.45, dirt=.8, moss=.1, wear=.3, param=1.0)
    add('paint-black', 0x232126, WD, smooth=.45, dirt=.8, moss=.1, wear=.3, param=1.0)
    add('paint-mustard', 0xB8892E, WD, smooth=.45, dirt=.8, moss=.1, wear=.3, param=1.0)
    add('paint-white', 0xE8E4D8, WD, smooth=.45, dirt=.7, wear=.25, param=1.0)       # sashes and glazing bars
    add('paint-plinth', 0x2A2A30, NO, smooth=.35, dirt=.9, moss=.2, wear=.2)
    add('timber-old', 0x8A6E52, WD, smooth=.15, dirt=1.0, moss=.3, wear=.1, param=1.0)
    add('ply', 0x9C8462, WD, smooth=.12, dirt=1.0, moss=.2, wear=.1, param=0.0)       # boards over a window
    add('tape', 0xC8B48A, NO, smooth=.3, dirt=.6)
    add('canvas-red', 0x9A3A2E, sk.DETAIL_CLOTH, smooth=.1, dirt=.8, moss=.1)
    add('canvas-cream', 0xE2D6B8, sk.DETAIL_CLOTH, smooth=.1, dirt=.8, moss=.1)
    add('canvas-green', 0x2E5E48, sk.DETAIL_CLOTH, smooth=.1, dirt=.8, moss=.1)
    add('curtain-a', 0x7A3A3A, sk.DETAIL_CLOTH, smooth=.1, dirt=.4)
    add('curtain-b', 0x3A4A6A, sk.DETAIL_CLOTH, smooth=.1, dirt=.4)
    add('curtain-c', 0x6A6040, sk.DETAIL_CLOTH, smooth=.1, dirt=.4)
    add('blind', 0xD8C8A0, sk.DETAIL_CLOTH, smooth=.15, dirt=.5)
    add('shutter', 0x7C848A, NO, smooth=.4, metal=.5, dirt=1.0, wear=.2)
    add('poster-a', 0xD8B64A, NO, smooth=.2, dirt=.9)
    add('poster-b', 0x4F7FA3, NO, smooth=.2, dirt=.9)
    add('poster-c', 0xC8553F, NO, smooth=.2, dirt=.9)
    add('poster-d', 0xE6DCC2, NO, smooth=.2, dirt=.9)
    # pass 2: the long night's weathering (ghost signs, boards, scars, plates)
    add('ghost-ground', 0x5E3B33, NO, smooth=.12, dirt=1.1, moss=.15)    # a ghost sign's faded oxblood ground on brick
    add('ghost-blue', 0x3E4A5E, NO, smooth=.12, dirt=1.1, moss=.15)      # a faded navy ground
    add('ghost-green', 0x3E5446, NO, smooth=.12, dirt=1.1, moss=.15)     # a faded municipal green ground
    add('ghost-cream', 0xC9BC98, NO, smooth=.12, dirt=1.0, wear=.3)       # the faded letters and border
    add('ghost-gold', 0xB89A56, NO, smooth=.12, dirt=1.0, wear=.3)        # faded gilt letters
    add('brick-infill', 0x7A4232, B, smooth=.18, dirt=1.1, moss=.25, wear=.1, param=0.0)  # a blind window's later brick
    add('render-patch', 0x9A8C76, PL, smooth=.12, dirt=1.3, moss=.25, wear=.1)            # a patched scar on a party wall
    add('flashing', 0x3E4046, NO, smooth=.3, metal=.3, dirt=.8)          # lead flashing and its tar line
    add('board-grey', 0x857C6C, WD, smooth=.12, dirt=1.1, moss=.25, wear=.15, param=0.0)  # weathered boards
    add('board-pale', 0xA8946E, WD, smooth=.12, dirt=1.0, moss=.15, wear=.15, param=0.0)  # newer boards
    add('spray-white', 0xF0EEE6, NO, smooth=.25, dirt=.4)                # the survivors' marks (bible 3.4)
    add('spray-orange', 0xFF8A3A, NO, smooth=.25, dirt=.4)
    add('copper-dark', 0x3E6E60, sk.DETAIL_BRASS, smooth=.3, metal=.3, dirt=.6, wear=.15)  # a dome's shaded ribs
    # glass (opaque in StageLit: dark and reflective, or lit)
    add('glass-dark', 0x1A2230, NO, smooth=.90, dirt=.2)
    add('glass-frosted', 0xB8C8CC, NO, smooth=.30, dirt=.3)
    add('interior', 0x14141A, NO, smooth=.1, dirt=0.0)                               # dark goods against a lit shop
    # the lights (bible 3.1, 3.2): glow gain on the glow sub-mesh
    glow('lit-tungsten', 0xFFB062, 2.2)
    glow('lit-bright', 0xFFD08A, 2.6)
    glow('lit-curtain', 0xE07A50, 1.8)
    glow('lit-tv', 0x86A8FF, 2.0)
    glow('lit-shop', 0xFFC98A, 2.6)
    glow('lit-cold', 0xDCEBFF, 2.2)
    glow('lit-stair', 0xFFD9A0, 2.4)
    glow('fanlight', 0xFFC27A, 2.2)
    glow('sodium', 0xFFC27A, 3.0)
    glow('lantern', 0xFFB054, 2.6)
    glow('clock-opal', 0xF2E6C6, 2.4)
    glow('beacon', 0xFF2433, 3.0)
    glow('neon-green', 0x36F08A, 4.0)
    glow('lit-blind', 0xF0C890, 1.9)        # a blind drawn over a lit room
    glow('flood', 0xFFD9A8, 2.6)            # a civic flood's lens (bible 3.2)
    glow('crest-opal', 0xF2E6C6, 1.3)       # the pediment crest's clock face, lit low (the dials' #F2E6C6)
    return F


def glow(name, hexcol, gain):
    """A glowing finish (the glow sub-mesh at `gain`)."""
    F[name] = sk.fin('mer-' + name, hexcol, sk.DETAIL_GLOW, smooth=0.0, dirt=0.0, glow=gain)
    GLOW[name] = gain
    return F[name]


def fin_name(f):
    return sk.FINS[f].name


# ---------------------------------------------------------------------------------------------------------------------------
# Frames
# ---------------------------------------------------------------------------------------------------------------------------
class Frame:
    """A facade's space: o + a u + b v + c n (u along the facade, v up, n = u x v out; never mirrored)."""

    def __init__(self, o, u, v=(0.0, 1.0, 0.0)):
        self.o = tuple(float(x) for x in o)
        self.u = _norm(u)
        self.v = _norm(v)
        self.n = _cross(self.u, self.v)

    def P(self, a, b, c=0.0):
        o, u, v, n = self.o, self.u, self.v, self.n
        return (o[0] + a * u[0] + b * v[0] + c * n[0], o[1] + a * u[1] + b * v[1] + c * n[1],
                o[2] + a * u[2] + b * v[2] + c * n[2])

    def D(self, a, b, c):
        u, v, n = self.u, self.v, self.n
        return (a * u[0] + b * v[0] + c * n[0], a * u[1] + b * v[1] + c * n[1], a * u[2] + b * v[2] + c * n[2])

    def rows(self):
        u, v, n, o = self.u, self.v, self.n, self.o
        return [u[0], v[0], n[0], o[0], u[1], v[1], n[1], o[1], u[2], v[2], n[2], o[2]]

    def shifted(self, a=0.0, b=0.0, c=0.0):
        return Frame(self.P(a, b, c), self.u, self.v)

    @property
    def mu(self):
        return (-self.u[0], -self.u[1], -self.u[2])

    @property
    def mv(self):
        return (-self.v[0], -self.v[1], -self.v[2])

    @property
    def mn(self):
        return (-self.n[0], -self.n[1], -self.n[2])


def frame_facing(nx, nz, x, z, y=0.0):
    """A frame at (x, y, z) whose n is (nx, 0, nz) (u = (nz, 0, -nx))."""
    return Frame((x, y, z), (nz, 0.0, -nx))


def fquad(g, fr, a0, b0, a1, b1, c, f, normal=None):
    """A rectangle in the frame's plane at depth c, (a0, b0) to (a1, b1), facing n (or `normal`)."""
    g.face((fr.P(a0, b0, c), fr.P(a1, b0, c), fr.P(a1, b1, c), fr.P(a0, b1, c)), f, normal or fr.n)


def fbox(g, T, fr, u0, u1, v0, v1, c0, c1, f, bevel=.012, cell=0, skip=()):
    """A box in a frame's coordinates (skip: '-x' u0, '+x' u1, '-y' v0, '+y' v1, '-z' c0, '+z' c1). Small boxes lose their
    chamfer at LOD1."""
    t = Geo()
    b = bevel if (T is None or T.bevel) else 0.0
    sk.box(t, u0, u1, v0, v1, c0, c1, f, T if b else None, b, cell=cell, skip=skip)
    g.extend(t.transformed(fr.rows()))


def bx(g, T, x0, x1, y0, y1, z0, z1, f, bevel=.02, skip=()):
    """An axis-aligned box for the tier: chamfered at LOD0 only; a large one cut into the tier's cells (vertex colours)."""
    big = max(abs(x1 - x0), abs(y1 - y0), abs(z1 - z0)) > 2.0
    sk.box(g, x0, x1, y0, y1, z0, z1, f, T, bevel if T.bevel else 0.0, cell=(T.cell_high if big else 0), skip=skip)


def fgeo(g, fr, t):
    g.extend(t.transformed(fr.rows()))


def prism(g, fr, poly, c0, c1, f, caps=(True, True), smooth=False):
    """A prism in a frame: the polygon [(a, b)] (convex or not, counter-clockwise or not) extruded from depth c0 to c1. Caps are
    fanned only for convex polygons; a concave one is given as its convex parts by the caller."""
    k = len(poly)
    area = 0.0
    for i in range(k):
        a, b = poly[i], poly[(i + 1) % k]
        area += a[0] * b[1] - b[0] * a[1]
    sgn = 1.0 if area > 0 else -1.0
    for i in range(k):
        (a0, b0), (a1, b1) = poly[i], poly[(i + 1) % k]
        da, db = a1 - a0, b1 - b0
        # outward in the (a, b) plane: for a counter-clockwise polygon, (db, -da)
        nn = fr.D(db * sgn, -da * sgn, 0.0)
        g.face((fr.P(a0, b0, c0), fr.P(a1, b1, c0), fr.P(a1, b1, c1), fr.P(a0, b0, c1)), f, nn)
    if caps[1]:
        g.face([fr.P(a, b, c1) for a, b in poly], f, fr.n if c1 >= c0 else fr.mn)
    if caps[0]:
        g.face([fr.P(a, b, c0) for a, b in poly], f, fr.mn if c1 >= c0 else fr.n)


# ---------------------------------------------------------------------------------------------------------------------------
# Walls with openings
# ---------------------------------------------------------------------------------------------------------------------------
class Hole:
    """An opening in a wall: u0..u1, v0..v1 to the springing, `rise` the arch over it (0: a flat head; a rise of half the
    width is a round head)."""

    def __init__(self, u0, u1, v0, v1, rise=0.0, depth=.18, keep=None):
        self.u0, self.u1, self.v0, self.v1, self.rise, self.depth = u0, u1, v0, v1, rise, depth
        # LOD1 lays shallow openings on the wall as panes (bible 10.2: the window grid as lit and dark panes); deep ones
        # (.4 m and more: arcades, louvred arches, great doors) and the regions a module keeps clear stay cut
        self.keep = (depth >= .4) if keep is None else keep

    @property
    def w(self):
        return self.u1 - self.u0

    @property
    def top_v(self):
        return self.v1 + self.rise

    def top(self, u):
        if self.rise <= 0:
            return self.v1
        L = (self.u1 - self.u0) / 2
        return self.v1 + sk.arch_height(L, self.rise, min(u - self.u0, self.u1 - u))

    def cuts(self, T):
        """The head's segments: a round head takes the tier's full arc, a segmental one fewer in proportion to its rise
        (pass 2: a 0.18 m rise over 1.1 m is four segments at LOD0, not six)."""
        if self.rise <= 0:
            return [self.u0, self.u1]
        if self.rise >= self.w * .45:
            n = T.arc
        else:
            n = max(3 if T.lod == 0 else 2, int(round(T.arc * self.rise / (self.w / 2))))
        return [self.u0 + (self.u1 - self.u0) * i / n for i in range(n + 1)]


def cut(T, h):
    """Is the opening cut through the wall at this tier?"""
    return T.lod == 0 or h.keep


MERGE_WALLS = True     # pass 2: merge a wall's grid cells into rectangles before cutting them to the cell size


def wall(g, T, fr, u0, u1, v0, v1, f, holes=(), c=0.0, bands=None, base=0.0, merge=None):
    """A wall face from (u0, v0) to (u1, v1) at depth c with its openings left out; cut into cells of .75 m to 6 m above the
    ground (`base` is the frame's height over the street) and 1.5 m above (LOD1: 5 m). bands: [(v_from, v_to, finish)] paint
    the wall in horizontal strips (polychrome bands) in the same plane, so nothing lies on anything.

    The openings' edges cut the face into a grid. Pass 1 drew every grid cell on its own; pass 2 (merge) first gathers the
    cells into rectangles of one finish on one side of the 6 m line (greedy: along u, then up), then cuts each rectangle to
    the cell size, so a pier between windows is one column of cells instead of a stack of slivers."""
    hs = [h for h in holes if cut(T, h) and h.u1 > u0 and h.u0 < u1 and h.top_v > v0 and h.v0 < v1]
    us = sorted(set([u0, u1] + [x for h in hs for x in (h.u0, h.u1) if u0 < x < u1]))
    six = 6.0 - base
    vs = sorted(set([v0, v1] + [x for h in hs for x in (h.v0, h.v1, h.top_v) if v0 < x < v1] +
                    ([x for b in (bands or ()) for x in b[:2] if v0 < x < v1]) + ([six] if v0 < six < v1 else [])))
    us = [x for k, x in enumerate(us) if k == 0 or x - us[k - 1] > 1e-5]
    vs = [x for k, x in enumerate(vs) if k == 0 or x - vs[k - 1] > 1e-5]
    n = fr.n
    nu_, nv_ = len(us) - 1, len(vs) - 1
    key = [[None] * nv_ for _ in range(nu_)]
    for i in range(nu_):
        um = (us[i] + us[i + 1]) / 2
        for j in range(nv_):
            vm = (vs[j] + vs[j + 1]) / 2
            if any(h.u0 < um < h.u1 and h.v0 < vm < h.top_v for h in hs):
                continue
            ff = f
            for (b0, b1, bf) in (bands or ()):
                if b0 <= vm < b1:
                    ff = bf
            key[i][j] = (ff, base + vm >= 6.0)
    rects = []
    if merge if merge is not None else MERGE_WALLS:
        done = [[False] * nv_ for _ in range(nu_)]
        for j in range(nv_):
            for i in range(nu_):
                if done[i][j] or key[i][j] is None:
                    continue
                k = key[i][j]
                i1 = i
                while i1 + 1 < nu_ and not done[i1 + 1][j] and key[i1 + 1][j] == k:
                    i1 += 1
                j1 = j
                while j1 + 1 < nv_ and all(not done[x][j1 + 1] and key[x][j1 + 1] == k for x in range(i, i1 + 1)):
                    j1 += 1
                for x in range(i, i1 + 1):
                    for y in range(j, j1 + 1):
                        done[x][y] = True
                rects.append((us[i], us[i1 + 1], vs[j], vs[j1 + 1], k[0]))
    else:
        for i in range(nu_):
            for j in range(nv_):
                if key[i][j] is not None:
                    rects.append((us[i], us[i + 1], vs[j], vs[j + 1], key[i][j][0]))
    for (ua, ub, va, vb, ff) in rects:
        cs = T.cell_at(base + (va + vb) / 2)
        nu = max(1, int(math.ceil((ub - ua) / cs - 1e-6)))
        nv = max(1, int(math.ceil((vb - va) / cs - 1e-6)))
        for a in range(nu):
            for b in range(nv):
                x0, x1 = ua + (ub - ua) * a / nu, ua + (ub - ua) * (a + 1) / nu
                y0, y1 = va + (vb - va) * b / nv, va + (vb - va) * (b + 1) / nv
                g.face((fr.P(x0, y0, c), fr.P(x1, y0, c), fr.P(x1, y1, c), fr.P(x0, y1, c)), ff, n)
    # the wall over an arched head, down to the arch
    for h in hs:
        if h.rise <= 0:
            continue
        cu = h.cuts(T)
        vt = h.top_v
        ff = f
        for (b0, b1, bf) in (bands or ()):
            if b0 <= (h.v1 + vt) / 2 < b1:
                ff = bf
        for k in range(len(cu) - 1):
            a, b = cu[k], cu[k + 1]
            g.face((fr.P(a, h.top(a), c), fr.P(b, h.top(b), c), fr.P(b, vt, c), fr.P(a, vt, c)), ff, n)


def reveal(g, T, fr, h, f, c=0.0, sill=True, head=True, depth=None):
    """An opening's reveal from the face (depth c) back by its depth: the jambs, the sill's floor and the head's soffit."""
    if not cut(T, h):
        return
    d = h.depth if depth is None else depth
    back = c - d
    g.face((fr.P(h.u0, h.v0, c), fr.P(h.u0, h.v0, back), fr.P(h.u0, h.v1, back), fr.P(h.u0, h.v1, c)), f, fr.u)
    g.face((fr.P(h.u1, h.v0, c), fr.P(h.u1, h.v0, back), fr.P(h.u1, h.v1, back), fr.P(h.u1, h.v1, c)), f, fr.mu)
    if sill:
        g.face((fr.P(h.u0, h.v0, c), fr.P(h.u1, h.v0, c), fr.P(h.u1, h.v0, back), fr.P(h.u0, h.v0, back)), f, fr.v)
    if head:
        cu = h.cuts(T)
        um = (h.u0 + h.u1) / 2
        for k in range(len(cu) - 1):
            a, b = cu[k], cu[k + 1]
            ya, yb = h.top(a), h.top(b)
            nn = fr.D(-(a + b - 2 * um) * .5 if h.rise > 0 else 0.0, -1.0 if h.rise <= 0 else -(ya + yb - 2 * h.v1) * .5 - .05, 0.0)
            g.face((fr.P(a, ya, c), fr.P(b, yb, c), fr.P(b, yb, back), fr.P(a, ya, back)), f, nn)


def back_fill(g, T, fr, h, depth, f):
    """The opening's back at `depth` (negative: behind the face), following an arched head; laid on the wall at LOD1."""
    if not cut(T, h):
        fquad(g, fr, h.u0, h.v0, h.u1, h.top_v, .02, f)
        return
    cu = h.cuts(T)
    for k in range(len(cu) - 1):
        a, b = cu[k], cu[k + 1]
        g.face((fr.P(a, h.v0, depth), fr.P(b, h.v0, depth), fr.P(b, h.top(b), depth), fr.P(a, h.top(a), depth)), f, fr.n)


# ---------------------------------------------------------------------------------------------------------------------------
# Mouldings: profiles run along a facade
# ---------------------------------------------------------------------------------------------------------------------------
def mould(g, T, fr, u0, u1, v, profile, f, ends=True, back=0.0):
    """A moulding along a facade from u0 to u1 at height v: profile [(out, up)] from the face (depth `back`), bottom to top.
    Its ends are closed by fanned faces in the u planes (convex profiles) or quads back to the face."""
    k = len(profile)
    pts0 = [fr.P(u0, v + b, back + a) for a, b in profile]
    pts1 = [fr.P(u1, v + b, back + a) for a, b in profile]
    for i in range(k - 1):
        da, db = profile[i + 1][0] - profile[i][0], profile[i + 1][1] - profile[i][1]
        if abs(da) + abs(db) < 1e-7:
            continue
        nn = fr.D(0, -da, db)
        g.face((pts0[i], pts1[i], pts1[i + 1], pts0[i + 1]), f, nn)
    if ends:
        for (pts, uu, nrm) in ((pts0, u0, fr.mu), (pts1, u1, fr.u)):
            for i in range(k - 1):
                a0, b0 = profile[i]
                a1, b1 = profile[i + 1]
                if a0 < 1e-5 and a1 < 1e-5:
                    continue
                g.face((fr.P(uu, v + b0, back + a0), fr.P(uu, v + b1, back + a1), fr.P(uu, v + b1, back), fr.P(uu, v + b0, back)), f, nrm)


def mould_lod(T, profile, keep=None):
    """A profile for the tier: LOD1 keeps the outline's few strongest points (`keep` indices, or the first, the outermost and
    the last)."""
    if T.lod == 0:
        return profile
    if keep is None:
        out = max(range(len(profile)), key=lambda i: profile[i][0])
        keep = sorted(set([0, out, len(profile) - 1]))
    return [profile[i] for i in keep]


# profiles: (out, up) from the face, bottom to top
P_STRING = [(0.0, 0.0), (.04, -.0), (.08, .03), (.08, .13), (.05, .16), (0.0, .18)]
P_SILL = [(0.0, -.10), (.10, -.10), (.10, -.085), (.135, -.085), (.135, -.02), (.11, 0.0), (0.0, .02)]
P_CORNICE = [(0.0, 0.0), (.06, 0.0), (.06, .08), (.14, .14), (.14, .2), (.30, .32), (.38, .36), (.42, .42), (.42, .5), (0.0, .5)]
P_CORNICE_BIG = [(0.0, 0.0), (.08, 0.0), (.08, .12), (.18, .2), (.18, .3), (.42, .48), (.52, .54), (.58, .62), (.58, .74), (0.0, .74)]
P_COPING = [(0.0, 0.0), (.06, 0.0), (.06, .04), (.05, .12), (-.0, .16)]
P_PLINTH_CAP = [(0.0, 0.0), (.06, 0.0), (.06, .02), (.02, .08), (0.0, .08)]
P_FASCIA_CORNICE = [(0.0, 0.0), (.05, 0.0), (.05, .05), (.16, .12), (.24, .16), (.24, .22), (0.0, .22)]
P_EAVES = [(0.0, 0.0), (.05, 0.0), (.05, .06), (.12, .1), (.12, .16), (.0, .16)]
P_HOOD = [(0.0, 0.0), (.05, 0.0), (.05, .03), (.12, .07), (.12, .1), (.06, .14), (0.0, .16)]
P_STRING_SMALL = [(0.0, 0.0), (.05, 0.0), (.05, .09), (0.0, .12)]


def smould(g, T, fr, u0, u1, v, profile, f, **kw):
    """A shallow moulding (string course, plinth cap, hood, small cornice): LOD0 only (bible 10.2)."""
    if T.lod == 0:
        mould(g, T, fr, u0, u1, v, profile, f, **kw)


P_SILL_LEAN = [(0.0, -.10), (.13, -.10), (.13, -.025), (.10, 0.0), (0.0, .02)]


def sill(g, T, fr, u0, u1, v, f, lean=False, slab=False):
    """A stone sill 12 cm proud with its drip, reaching 8 cm past the opening each side (bible 2.3). LOD0 only. lean (pass 2,
    for windows above the ground floors of tall faces): the same block without the drip's step, 20 triangles not 32; its
    shade line under it, which is what reads from the street, is the same."""
    if T.lod > 0:
        return
    if slab:
        # board 3 (the dressed tenement chunk's cap): high on a tall face, a plain 12 cm slab, 10 triangles; its shade line
        # under it is what reads from the street
        fbox(g, T, fr, u0 - .08, u1 + .08, v - .10, v + .01, 0.0, .12, f, 0, skip=('-z',))
        return
    mould(g, T, fr, u0 - .08, u1 + .08, v, P_SILL_LEAN if lean else mould_lod(T, P_SILL, [0, 1, 4, 6]), f)


def cornice(g, T, fr, u0, u1, v, f, big=False, consoles=0.0, fc=None):
    """A cornice at height v (its foot); consoles (corbels under it) every `consoles` m at LOD0."""
    pr = P_CORNICE_BIG if big else P_CORNICE
    mould(g, T, fr, u0, u1, v, mould_lod(T, pr), f)
    if consoles > 0 and T.lod == 0:
        n = max(1, int(round((u1 - u0) / consoles)))
        for i in range(n):
            x = u0 + (u1 - u0) * (i + .5) / n
            console(g, T, fr, x, v, .16 if big else .12, .34 if big else .26, .22 if big else .14, fc or f)


def console(g, T, fr, u, v_top, w, h, out, f):
    """A console bracket under a ledge: a scrolled block (an S profile in side view), its top at v_top, `out` proud.
    LOD0 only."""
    if T.lod > 0:
        return
    prof = [(0.0, -h), (out * .35, -h), (out * .55, -h * .82), (out * .62, -h * .6), (out * .75, -h * .42), (out, -h * .22),
            (out, 0.0), (0.0, 0.0)]
    if T.lod > 0:
        prof = [(0.0, -h), (out * .4, -h), (out, -h * .25), (out, 0.0), (0.0, 0.0)]
    poly = [(c, v_top + b) for c, b in prof]
    # the profile lies in the (n, v) plane: build in a side frame whose u is the facade's n
    side = Frame(fr.P(u - w / 2, 0.0, 0.0), fr.mn, fr.v)  # u' = -n, v' = v -> n' = -n x v = ... (the facade's u)
    # in `side`, a = -c (depth), b = v, depth along the facade's u
    poly2 = [(-c, b) for c, b in poly]
    prism(g, side, poly2, 0.0, w, f)


# ---------------------------------------------------------------------------------------------------------------------------
# Windows and doors
# ---------------------------------------------------------------------------------------------------------------------------
def frame_ring(g, fr, u0, u1, v0, v1, w, c0, c1, f, inner=True, sides=(1, 1, 1, 1)):
    """A rectangular frame (a ring of bars `w` wide) in a frame's plane from depth c0 (back) to c1 (front): its front faces and,
    if `inner`, the faces lining its opening."""
    a0, a1, b0, b1 = u0 + w, u1 - w, v0 + w, v1 - w
    n = fr.n
    if sides[0]:
        fquad(g, fr, u0, v0, a0, v1, c1, f)                         # left stile (low u)
    if sides[1]:
        fquad(g, fr, a1, v0, u1, v1, c1, f)                         # right stile
    if sides[2]:
        fquad(g, fr, a0, v0, a1, b0, c1, f)                         # bottom rail
    if sides[3]:
        fquad(g, fr, a0, b1, a1, v1, c1, f)                         # top rail
    if inner and c1 - c0 > 1e-4:
        g.face((fr.P(a0, b0, c0), fr.P(a0, b1, c0), fr.P(a0, b1, c1), fr.P(a0, b0, c1)), f, fr.u)
        g.face((fr.P(a1, b0, c0), fr.P(a1, b1, c0), fr.P(a1, b1, c1), fr.P(a1, b0, c1)), f, fr.mu)
        g.face((fr.P(a0, b0, c0), fr.P(a1, b0, c0), fr.P(a1, b0, c1), fr.P(a0, b0, c1)), f, fr.v)
        g.face((fr.P(a0, b1, c0), fr.P(a1, b1, c0), fr.P(a1, b1, c1), fr.P(a0, b1, c1)), f, fr.mv)
    return a0, a1, b0, b1


def bar(g, T, fr, u0, u1, v0, v1, c0, c1, f):
    """A glazing bar or rail: its front and its two long sides (no ends, no back)."""
    if u1 - u0 > v1 - v0:
        fquad(g, fr, u0, v0, u1, v1, c1, f)
        g.face((fr.P(u0, v0, c0), fr.P(u1, v0, c0), fr.P(u1, v0, c1), fr.P(u0, v0, c1)), f, fr.mv)
        g.face((fr.P(u0, v1, c0), fr.P(u1, v1, c0), fr.P(u1, v1, c1), fr.P(u0, v1, c1)), f, fr.v)
    else:
        fquad(g, fr, u0, v0, u1, v1, c1, f)
        g.face((fr.P(u0, v0, c0), fr.P(u0, v1, c0), fr.P(u0, v1, c1), fr.P(u0, v0, c1)), f, fr.mu)
        g.face((fr.P(u1, v0, c0), fr.P(u1, v1, c0), fr.P(u1, v1, c1), fr.P(u1, v0, c1)), f, fr.u)


WINDOW_FILLS = ('dark', 'curtain', 'lit', 'lit-curtain', 'lit-blind', 'blind', 'board', 'tape', 'tv', 'broken')


LIT_FILLS = ('lit', 'lit-bright', 'lit-curtain', 'lit-blind', 'tv', 'lit-stair')


def lit_finish(fill, rnd=None):
    """The glow a lit window takes (decided with its fill, so every tier agrees)."""
    return {'tv': F['lit-tv'], 'lit-curtain': F['lit-curtain'], 'lit-bright': F['lit-bright'],
            'lit-blind': F['lit-blind'], 'lit-stair': F['lit-stair']}.get(fill, F['lit-tungsten'])


def key_of(fr, a, b):
    """A window's stable key: where it is (so a fill is the same at every tier)."""
    p = fr.P(a, b, 0.0)
    return (round(p[0], 1), round(p[1], 1), round(p[2], 1))


def pane(g, T, fr, u0, u1, v0, v1, c, fill, rnd, lit=None, lower=True):
    """One pane of glass at depth c and what is behind or on it: dark glass, curtains, a lit room (now and then a lampshade or
    a plant against the light, in the lower sash; never a figure, bible 2.7), a blind (dark, or lit from behind), tape, a
    smashed pane."""
    if fill in LIT_FILLS:
        fquad(g, fr, u0, v0, u1, v1, c, lit if lit is not None else lit_finish(fill, rnd))
    else:
        fquad(g, fr, u0, v0, u1, v1, c, F['glass-dark'])
    if not T.detail:
        return
    w = u1 - u0
    if fill in ('curtain', 'lit-curtain'):
        # drawn curtains as two dark panels in front of the glass, a gap between them
        fc = F[rnd.choice(('curtain-a', 'curtain-b', 'curtain-c'))]
        gap = rnd.uniform(.15, .45) * w
        lw = (w - gap) * rnd.uniform(.35, .65)
        fquad(g, fr, u0, v0, u0 + lw, v1, c + .004, fc)
        fquad(g, fr, u0 + lw + gap, v0, u1, v1, c + .004, fc)
    elif fill == 'blind':
        y = v1 - (v1 - v0) * rnd.uniform(.3, .7)
        fquad(g, fr, u0, y, u1, v1, c + .004, F['blind'])
    elif fill == 'lit-blind':
        # the blind drawn most of the way down, lit from behind, its bottom rail dark
        y = v0 + (v1 - v0) * (rnd.uniform(.12, .35) if lower else 0.0)
        fquad(g, fr, u0, y, u1, v1, c + .004, F['lit-blind'])
        if lower:
            fquad(g, fr, u0, y - .03, u1, y, c + .006, F['blind'])
    elif fill in ('lit', 'lit-bright'):
        k = rnd.random()
        x = u0 + w * rnd.uniform(.25, .75)
        if lower and k < .2:
            # a lampshade on a table
            fquad(g, fr, x - .015, v0, x + .015, v0 + .14, c + .004, F['interior'])
            g.face((fr.P(x - .13, v0 + .14, c + .004), fr.P(x + .13, v0 + .14, c + .004), fr.P(x + .08, v0 + .3, c + .004),
                    fr.P(x - .08, v0 + .3, c + .004)), F['interior'], fr.n)
        elif lower and k < .35:
            # a pot plant on the sill
            fquad(g, fr, x - .07, v0, x + .07, v0 + .1, c + .004, F['interior'])
            for (dx, dy, s_) in ((0.0, .2, .1), (-.07, .15, .07), (.07, .17, .08)):
                g.face([fr.P(x + dx + s_ * math.cos(a), v0 + .1 + dy + s_ * math.sin(a), c + .004)
                        for a in [2 * math.pi * q / 6 for q in range(6)]], F['interior'], fr.n)
    elif fill == 'broken' and lower:
        # a pane smashed in: the dark room behind a jagged hole (a fan, so any outline is drawn right)
        cx, cy = u0 + w * rnd.uniform(.3, .7), v0 + (v1 - v0) * rnd.uniform(.3, .7)
        rr = min(w, v1 - v0) * rnd.uniform(.25, .42)
        pts = []
        for i in range(8):
            a = 2 * math.pi * i / 8 + rnd.uniform(-.2, .2)
            q = rr * (rnd.uniform(.45, .7) if i % 2 else rnd.uniform(.85, 1.15))
            pts.append(fr.P(min(u1, max(u0, cx + q * math.cos(a))), min(v1, max(v0, cy + q * math.sin(a))), c + .004))
        C = fr.P(cx, cy, c + .004)
        for i in range(8):
            g.face((C, pts[i], pts[(i + 1) % 8]), F['interior'], fr.n)


def sash_window(g, T, fr, h, fill='dark', rnd=None, frame_fin=None, bars=True, c=0.0, simple=False, mark=None):
    """A two-over-two box sash in the opening `h` (bible 5.1): the box frame 12 cm wide set back in the reveal, the upper sash
    proud of the lower, a meeting rail, one glazing bar each (LOD0), the panes and their fill. A boarded fill is planks
    (`boarded`, with a sprayed `mark` now and then); a taped one an X. `simple` (pass 2, for windows high up a tall face and
    on rear walls): the box frame, the meeting rail and the two panes only."""
    rnd = rnd or random.Random(0)
    fw = frame_fin if frame_fin is not None else F['paint-white']
    if not cut(T, h):
        # LOD1: the window laid on the wall: its frame, and the pane inset as LOD0's glass is (so a lit window keeps its
        # glowing share at the swap), lit or dark; or its boards
        if fill == 'board':
            fquad(g, fr, h.u0, h.v0, h.u1, h.top_v, c + .02, F['board-grey'])
            return
        fquad(g, fr, h.u0, h.v0, h.u1, h.top_v, c + .02, fw)
        i = min(.14, (h.u1 - h.u0) * .22)
        ff = lit_finish(fill, rnd) if fill in LIT_FILLS else F['glass-dark']
        fquad(g, fr, h.u0 + i, h.v0 + i, h.u1 - i, h.top_v - i, c + .03, ff)
        return
    d = h.depth
    fb = c - d + .02         # the frame's back (against the reveal's back wall)
    ff = c - d + .085        # the frame's front
    top = h.v1  # sashes stop at the springing; an arched head takes a fixed fanlight-like pane
    if fill == 'board':
        boarded(g, T, fr, h, c, rnd, mark)
        return
    w = .12 if T.lod == 0 else .1
    a0, a1, b0, b1 = frame_ring(g, fr, h.u0, h.u1, h.v0, top, w, fb, ff, fw)
    litf = lit_finish(fill, rnd) if fill in LIT_FILLS else None
    if h.rise > 0:
        # the arched head's fixed pane over the transom
        hh = Hole(h.u0, h.u1, top, top, h.rise, d)
        cu = hh.cuts(T)
        for k in range(len(cu) - 1):
            a, b = cu[k], cu[k + 1]
            g.face((fr.P(a, top, ff), fr.P(b, top, ff), fr.P(b, hh.top(b), ff), fr.P(a, hh.top(a), ff)),
                   litf if litf is not None else F['glass-dark'], fr.n)
    mid = (b0 + b1) / 2
    curtains = rnd.getstate()
    if simple:
        bar(g, T, fr, a0, a1, mid - .03, mid + .03, ff - .06, ff - .015, fw)
        for (sv0, sv1, lower) in ((b0, mid - .03, True), (mid + .03, b1, False)):
            rnd.setstate(curtains)
            pane(g, T, fr, a0, a1, sv0, sv1, ff - .05, fill, rnd, litf, lower)
        return
    sw = .055 if T.lod == 0 else 0.0
    # lower sash (behind), upper sash (in front by 4 cm)
    for (sv0, sv1, cc, lower) in ((b0, mid + .02, ff - .045, True), (mid - .02, b1, ff - .005, False)):
        if T.lod == 0:
            frame_ring(g, fr, a0, a1, sv0, sv1, sw, cc - .03, cc, fw)
            pa0, pa1, pb0, pb1 = a0 + sw, a1 - sw, sv0 + sw, sv1 - sw
        else:
            pa0, pa1, pb0, pb1 = a0, a1, sv0, sv1
        if bars and T.lod == 0:
            xm = (pa0 + pa1) / 2
            bar(g, T, fr, xm - .0225, xm + .0225, pb0, pb1, cc - .03, cc - .004, fw)
        rnd.setstate(curtains)  # both sashes draw the same curtains
        pane(g, T, fr, pa0, pa1, pb0, pb1, cc - .03, fill, rnd, litf, lower)
    if fill == 'tape' and T.detail:
        # an X of tape across the lower sash (near Ward Zero)
        for (sa, sb) in ((0, 1), (1, 0)):
            p0 = (a0 + .03, b0 + .03) if sa == 0 else (a1 - .03, b0 + .03)
            p1 = (a1 - .03, mid - .03) if sa == 0 else (a0 + .03, mid - .03)
            dx, dy = p1[0] - p0[0], p1[1] - p0[1]
            L = math.hypot(dx, dy)
            px, py = -dy / L * .03, dx / L * .03
            cc = ff - .045 - .025
            g.face((fr.P(p0[0] + px, p0[1] + py, cc), fr.P(p1[0] + px, p1[1] + py, cc), fr.P(p1[0] - px, p1[1] - py, cc),
                    fr.P(p0[0] - px, p0[1] - py, cc)), F['tape'], fr.n)


def panel_door(g, T, fr, u0, u1, v0, v1, c, f, rnd, panels=4, knocker=True):
    """A panelled door leaf (its face at depth c): stiles, rails and raised fields at LOD0, a brass knob, letterbox and
    knocker."""
    fquad(g, fr, u0, v0, u1, v1, c, f)
    if not T.detail:
        return
    w = u1 - u0
    hgt = v1 - v0
    # the fields: two columns; rows (lower short, upper tall) for four panels, three rows for six
    rows = ((.08, .36), (.44, .92)) if panels == 4 else ((.07, .3), (.36, .62), (.68, .93))
    for (p, q) in rows:
        for (a, b) in ((.14, .46), (.54, .86)):
            x0, x1, y0, y1 = u0 + w * a, u0 + w * b, v0 + hgt * p, v0 + hgt * q
            # a raised field: its bevelled edge rising from the leaf to the field's flat
            e = .035
            fquad(g, fr, x0 + e, y0 + e, x1 - e, y1 - e, c + .018, f)
            g.face((fr.P(x0, y0, c), fr.P(x1, y0, c), fr.P(x1 - e, y0 + e, c + .018), fr.P(x0 + e, y0 + e, c + .018)), f, fr.D(0, -.5, 1))
            g.face((fr.P(x0, y1, c), fr.P(x1, y1, c), fr.P(x1 - e, y1 - e, c + .018), fr.P(x0 + e, y1 - e, c + .018)), f, fr.D(0, .5, 1))
            g.face((fr.P(x0, y0, c), fr.P(x0, y1, c), fr.P(x0 + e, y1 - e, c + .018), fr.P(x0 + e, y0 + e, c + .018)), f, fr.D(-.5, 0, 1))
            g.face((fr.P(x1, y0, c), fr.P(x1, y1, c), fr.P(x1 - e, y1 - e, c + .018), fr.P(x1 - e, y0 + e, c + .018)), f, fr.D(.5, 0, 1))
    # the brass: knob, letterbox, knocker
    kx = u0 + w * .82
    sk.lathe(g, [(.035, 0.0), (.035, .02), (.03, .045), (0.0, .06)], fr.P(kx, v0 + 1.02, c), F['brass'], T.small, axis=fr.n)
    fbox(g, T, fr, u0 + w * .33, u0 + w * .67, v0 + hgt * .39 - .03, v0 + hgt * .39 + .03, c, c + .015, F['brass'], 0)
    if knocker:
        kx2 = u0 + w * .5
        ky = v0 + hgt * .62
        sk.lathe(g, [(.065, -.0), (.065, .012), (.045, .02), (.045, .0)], fr.P(kx2, ky - .055, c), F['brass'], 8, axis=fr.n)
        fbox(g, T, fr, kx2 - .03, kx2 + .03, ky - .01, ky + .05, c, c + .025, F['brass'], 0)


# ---------------------------------------------------------------------------------------------------------------------------
# The glyph kit (bible 4.3 rule 6, 10.5): a 5 x 7 stroke font, its strokes merged
# ---------------------------------------------------------------------------------------------------------------------------
GLYPHS = {
    'A': '01110 10001 10001 11111 10001 10001 10001', 'B': '11110 10001 10001 11110 10001 10001 11110',
    'C': '01111 10000 10000 10000 10000 10000 01111', 'D': '11110 10001 10001 10001 10001 10001 11110',
    'E': '11111 10000 10000 11110 10000 10000 11111', 'F': '11111 10000 10000 11110 10000 10000 10000',
    'G': '01111 10000 10000 10111 10001 10001 01111', 'H': '10001 10001 10001 11111 10001 10001 10001',
    'I': '111 010 010 010 010 010 111', 'J': '00111 00010 00010 00010 00010 10010 01100',
    'K': '10001 10010 10100 11000 10100 10010 10001', 'L': '10000 10000 10000 10000 10000 10000 11111',
    'M': '10001 11011 10101 10101 10001 10001 10001', 'N': '10001 11001 10101 10011 10001 10001 10001',
    'O': '01110 10001 10001 10001 10001 10001 01110', 'P': '11110 10001 10001 11110 10000 10000 10000',
    'Q': '01110 10001 10001 10001 10101 10010 01101', 'R': '11110 10001 10001 11110 10100 10010 10001',
    'S': '01111 10000 10000 01110 00001 00001 11110', 'T': '11111 00100 00100 00100 00100 00100 00100',
    'U': '10001 10001 10001 10001 10001 10001 01110', 'V': '10001 10001 10001 10001 01010 01010 00100',
    'W': '10001 10001 10001 10101 10101 11011 10001', 'X': '10001 01010 00100 00100 00100 01010 10001',
    'Y': '10001 10001 01010 00100 00100 00100 00100', 'Z': '11111 00001 00010 00100 01000 10000 11111',
    '0': '01110 10011 10101 10101 10101 11001 01110', '1': '00100 01100 00100 00100 00100 00100 01110',
    '2': '01110 10001 00001 00110 01000 10000 11111', '3': '11110 00001 00001 01110 00001 00001 11110',
    '4': '00010 00110 01010 10010 11111 00010 00010', '5': '11111 10000 11110 00001 00001 10001 01110',
    '6': '00110 01000 10000 11110 10001 10001 01110', '7': '11111 00001 00010 00100 01000 01000 01000',
    '8': '01110 10001 10001 01110 10001 10001 01110', '9': '01110 10001 10001 01111 00001 00010 01100',
    ' ': '000 000 000 000 000 000 000', '.': '0 0 0 0 0 0 1', '-': '000 000 000 111 000 000 000',
    '&': '01100 10010 10100 01000 10101 10010 01101', "'": '1 1 0 0 0 0 0', '/': '00001 00010 00010 00100 01000 01000 10000',
    ':': '0 1 0 0 0 1 0', '*': '0 0 0 1 0 0 0',  # '*' is the middle dot
}


def glyph_rects(ch):
    """The glyph's strokes as rectangles (col0, col1, row0, row1) in cells, rows from the top; runs merged downwards."""
    rows = GLYPHS.get(ch, GLYPHS[' ']).split(' ')
    rects = []
    open_ = {}
    for r, row in enumerate(rows):
        runs = []
        i = 0
        while i < len(row):
            if row[i] == '1':
                j = i
                while j < len(row) and row[j] == '1':
                    j += 1
                runs.append((i, j))
                i = j
            else:
                i += 1
        nxt = {}
        for run in runs:
            if run in open_:
                nxt[run] = open_.pop(run)
            else:
                nxt[run] = r
        for run, r0 in open_.items():
            rects.append((run[0], run[1], r0, r))
        open_ = nxt
    for run, r0 in open_.items():
        rects.append((run[0], run[1], r0, len(rows)))
    return rects, len(rows[0])


def text_width(s, px):
    w = 0.0
    for ch in s:
        w += (len(GLYPHS.get(ch, GLYPHS[' ']).split(' ')[0]) + 1) * px
    return w - px


def text(g, T, fr, u, v, px, s, f, c=0.0, depth=.02, centre=True, raised=True):
    """Lettering on a facade, read from the street: 5 x 7 strokes `px` m a cell, standing `depth` proud of depth c, its foot at
    v, centred on u (or starting at u, the reader's left). LOD0 strokes are raised blocks; LOD1 keeps flat strokes only for a
    glowing finish (the caller drops painted letters at LOD1)."""
    W = text_width(s, px)
    x = -W / 2 if centre else 0.0  # the reader's x from u (reader's right is -u)
    for ch in s:
        rects, cols = glyph_rects(ch)
        for (c0, c1, r0, r1) in rects:
            # reader's left-to-right x maps to u decreasing
            ua, ub = u - (x + c1 * px), u - (x + c0 * px)
            va, vb = v + (7 - r1) * px, v + (7 - r0) * px
            if raised and T.lod == 0:
                fbox(g, T, fr, ua, ub, va, vb, c, c + depth, f, 0, skip=('-z',))
            else:
                fquad(g, fr, ua, va, ub, vb, c + (depth if raised else .004), f)
        x += (cols + 1) * px
    return W


# ---------------------------------------------------------------------------------------------------------------------------
# Swept and turned parts, turned OUT (pass 2: the fix for stagekit's inside-out sweeps, kept here so the older stages are
# untouched)
# ---------------------------------------------------------------------------------------------------------------------------
def _sc(a, k):
    return (a[0] * k, a[1] * k, a[2] * k)


def sweep(g, section, path, f, closed=False, up=None, caps=True, smooth_u=True, scale=None):
    """A section [(x, y)] swept along a path of points (Unity space), as stagekit.sweep: rotation-minimising frames from
    `up` (or the path's first normal), x along the frame's u and y along its v, `scale` per point; but with every face
    turned OUT of the section whichever way the section winds. (stagekit.sweep's sign is reversed for a counter-clockwise
    section and its faces are wound inward for a clockwise one, so everything it sweeps is inside out under the game's
    back-face culling: measured 2026-11-19 by checks/inside_out.py on the ridges, copings, arms and ribs.) A corner is
    mitred: its section lies across the bisector, widened in the bend's plane so every side keeps its true width
    (stagekit's chord tangent narrowed a closed coping's short sides to half and left a slot over the parapet's inner
    face)."""
    m = len(path)
    if m < 2:
        return

    def unit(a):
        ll = math.sqrt(_dot(a, a))
        return (a[0] / ll, a[1] / ll, a[2] / ll) if ll > 1e-9 else None

    tg, mit = [], []
    rmax = max(math.hypot(x, y) for (x, y) in section)
    for i in range(m):
        d0 = unit(_sub(path[i], path[(i - 1) % m])) if (closed or i > 0) else None
        d1 = unit(_sub(path[(i + 1) % m], path[i])) if (closed or i < m - 1) else None
        t = unit(_add(d0, d1)) if (d0 is not None and d1 is not None) else None
        if t is None:                        # an end, a doubled point or a path doubling back: no mitre
            t = d1 or d0 or (0.0, 0.0, 1.0)
            mit.append(None)
        else:
            c = _dot(t, d0)
            nb = unit(_sub(d1, d0))
            # (board 3: no mitre where it would reach past half the shorter neighbouring segment, as on a tight scroll's
            # short steps: there the widened ring overlapped the next one and folded its faces inward)
            s_i = scale[i] if scale is not None else 1.0
            reach = rmax * s_i * math.sqrt(max(0.0, 1.0 / max(c, .25) ** 2 - 1.0))
            short = min(math.dist(path[i], path[(i - 1) % m]), math.dist(path[(i + 1) % m], path[i]))
            fold = reach > .5 * short
            if c < .35:
                # (board 3: a near reversal, a scroll's hook: the bisector lies across both segments and twists the next
                # faces; take the chord's tangent, as stagekit does, and no mitre)
                tc = unit(_sub(path[(i + 1) % m], path[(i - 1) % m]))
                if tc is not None:
                    t = tc
                fold = True
            mit.append((nb, 1.0 / max(c, .25)) if (nb is not None and c < .9999 and not fold) else None)
        tg.append(t)
    ref = up if up is not None else ((0, 1, 0) if abs(tg[0][1]) < .95 else (1, 0, 0))
    u = _norm(_cross(ref, tg[0]))
    v = _cross(tg[0], u)
    frames = [(u, v)]
    for i in range(1, m):
        d1 = _sub(path[i], path[i - 1])
        c1 = _dot(d1, d1)
        if c1 < 1e-14:
            frames.append(frames[-1])
            continue
        uL = _add(u, d1, -2.0 / c1 * _dot(d1, u))
        tL = _add(tg[i - 1], d1, -2.0 / c1 * _dot(d1, tg[i - 1]))
        d2 = _sub(tg[i], tL)
        c2 = _dot(d2, d2)
        u = _norm(_add(uL, d2, -2.0 / c2 * _dot(d2, uL)) if c2 > 1e-14 else uL)
        v = _cross(tg[i], u)
        frames.append((u, v))
    k = len(section)
    area = sum(section[j][0] * section[(j + 1) % k][1] - section[(j + 1) % k][0] * section[j][1] for j in range(k))
    sgn = 1.0 if area > 0 else -1.0          # (dy, -dx) along a counter-clockwise outline points out of it
    sn, en = [], []
    for j in range(k):
        a, b = section[(j - 1) % k], section[(j + 1) % k]
        dx, dy = b[0] - a[0], b[1] - a[1]
        L = math.hypot(dx, dy) or 1.0
        sn.append((sgn * dy / L, -sgn * dx / L))         # a corner's normal (smooth round the section)
        a, b = section[j], section[(j + 1) % k]
        dx, dy = b[0] - a[0], b[1] - a[1]
        L = math.hypot(dx, dy) or 1.0
        en.append((sgn * dy / L, -sgn * dx / L))         # a side's own normal (flat sides)
    rings, nrings = [], []
    for i in range(m):
        u, v = frames[i]
        s_ = scale[i] if scale is not None else 1.0
        ring = []
        for (x, y) in section:
            w = _add(_sc(u, x * s_), v, y * s_)
            if mit[i] is not None:           # the mitre: the offset across the bend grows by 1 / cos(half the turn)
                nb, k_ = mit[i]
                w = _add(w, nb, _dot(w, nb) * (k_ - 1.0))
            ring.append(_add(path[i], w))
        rings.append(ring)
        nrings.append([_norm(_add(_sc(u, nx), v, ny)) for (nx, ny) in sn])
    count = m if closed else m - 1
    for i in range(count):
        i1 = (i + 1) % m
        for j in range(k):
            j1 = (j + 1) % k
            pts = (rings[i][j], rings[i][j1], rings[i1][j1], rings[i1][j])
            if smooth_u:
                g.face(pts, f, normals=(nrings[i][j], nrings[i][j1], nrings[i1][j1], nrings[i1][j]))
            else:
                (u0_, v0_), (u1_, v1_) = frames[i], frames[i1]
                nx, ny = en[j]
                g.face(pts, f, normal=_norm(_add(_add(_sc(u0_, nx), v0_, ny), _add(_sc(u1_, nx), v1_, ny))))
    if caps and not closed:
        g.face(rings[0], f, normal=_sc(tg[0], -1.0))
        g.face(rings[-1], f, normal=tg[-1])


def tube(g, path, r, f, sides, closed=False, caps=True):
    """A round bar or pipe along a path, turned out (a square one at four sides, its flats on the frame's axes)."""
    if len(path) < 2:
        return
    sweep(g, sk.circle_section(r, sides, math.pi / sides), path, f, closed=closed, caps=caps)


def lathe_shell(g, profile, centre, f, sides, thick=.012, axis=(0, 1, 0), f_in=None, smooth=True):
    """A thin turned shell seen from both sides (a lamp's dish shade, a cupola's canopy): `profile` [(radius, height)] walked
    from the rim to the apex along its OUTER face (so stagekit.lathe turns that face out), the inner face `thick` below it
    turned in, and the rim between them."""
    sk.lathe(g, profile, centre, f, sides, axis=axis, smooth=smooth)
    inner = [(r, h - thick) for (r, h) in profile][::-1]
    sk.lathe(g, inner, centre, f_in if f_in is not None else f, sides, axis=axis, smooth=smooth)
    r0, h0 = profile[0]
    sk.lathe(g, [(r0, h0 - thick), (r0, h0)], centre, f, sides, axis=axis, smooth=False)


# ---------------------------------------------------------------------------------------------------------------------------
# The long night's marks (pass 2): ghost signs, pattress plates, boarded windows, spray
# ---------------------------------------------------------------------------------------------------------------------------
def ghost_sign(g, T, fr, u0, u1, v0, v1, lines, ground='ghost-ground', ink='ghost-cream', rnd=None, c=.003, streaks=None):
    """A painted advertisement faded on a brick wall (bible 5.4): its ground in a few patches with bare brick where the paint
    has flaked, a border, and lines of painted letters [(text, cell px)] laid top to bottom (letters 7 cells tall: .05 m a
    cell is .35 m, the bible's least painted letter). Flat paint (6 mm), so it may stand anywhere. LOD1 keeps the ground
    (a colour mass) and drops the letters (bible 4.3 rule 6)."""
    rnd = rnd or random.Random(0)
    fg, fi = F[ground], F[ink]
    if T.lod > 0:
        fquad(g, fr, u0, v0, u1, v1, c, fg)
        # board 2 (the critique: the letters popped at the 38 m swap, leaving a blank panel, though letters 0.5 m and taller
        # read to about 70 m): such lines keep their strokes at LOD1, flat, on the ground's one quad
        e, w = .12, .07
        hs = [7 * px for _, px in lines]
        gap = max(.08, ((v1 - v0) - 2 * (e + w) - sum(hs)) / (len(lines) + 1))
        y = v1 - e - w - gap
        um = (u0 + u1) / 2
        for (s, px), hh in zip(lines, hs):
            y -= hh
            if hh >= .5:
                text(g, T, fr, um, y, px, s, fi, c=c + .0015, depth=.0015, raised=False)
            y -= gap
        return
    # the ground, flaked: cut into a few columns, one of them broken by a bare patch
    n = max(2, int((u1 - u0) / 2.2))
    flake = rnd.randrange(n)
    for i in range(n):
        a, b = u0 + (u1 - u0) * i / n, u0 + (u1 - u0) * (i + 1) / n
        if i == flake:
            y0 = v0 + (v1 - v0) * rnd.uniform(.05, .3)
            y1 = y0 + (v1 - v0) * rnd.uniform(.15, .3)
            fquad(g, fr, a, v0, b, y0, c, fg)
            fquad(g, fr, a, y1, b, v1, c, fg)
            fquad(g, fr, a + (b - a) * rnd.uniform(.4, .7), y0, b, y1, c, fg)
        else:
            fquad(g, fr, a, v0, b, v1, c, fg)
    # the border: a painted rule .07 m wide, inset .12 m
    e, w = .12, .07
    for (a, b, y0, y1) in ((u0 + e, u1 - e, v0 + e, v0 + e + w), (u0 + e, u1 - e, v1 - e - w, v1 - e),
                           (u0 + e, u0 + e + w, v0 + e + w, v1 - e - w), (u1 - e - w, u1 - e, v0 + e + w, v1 - e - w)):
        fquad(g, fr, a, y0, b, y1, c + .0015, fi)
    # the lines of lettering, centred, spread down the panel
    hs = [7 * px for _, px in lines]
    gap = max(.08, ((v1 - v0) - 2 * (e + w) - sum(hs)) / (len(lines) + 1))
    y = v1 - e - w - gap
    um = (u0 + u1) / 2
    for (s, px), hh in zip(lines, hs):
        y -= hh
        text(g, T, fr, um, y, px, s, fi, c=c + .0015, depth=.0015, raised=False)
        y -= gap
    if streaks is not None:
        streaks.add_under(fr, u0 + .2, u1 - .2, v0, 1.6, .22)


def pattress(g, T, fr, u, v, f=None, kind='disc'):
    """A tie-rod's cast-iron pattress plate on a wall at (u, v): a dished disc with its nut, or an S iron. LOD0 only."""
    if T.lod > 0:
        return
    f = F['iron-black'] if f is None else f
    if kind == 'disc':
        sk.lathe(g, [(.16, 0.0), (.15, .025), (.05, .04), (0.0, .07)], fr.P(u, v, 0.0), f, 6, axis=fr.n, smooth=False)
    else:
        # an S: a bar with its ends turned
        tube(g, sk.catmull([fr.P(u - .05, v - .32, .025), fr.P(u + .08, v - .18, .03), fr.P(u, v, .035),
                            fr.P(u - .08, v + .18, .03), fr.P(u + .05, v + .32, .025)], 2), .022, f, 4)
        sk.lathe(g, [(.045, 0.0), (.045, .05), (0.0, .06)], fr.P(u, v, 0.0), f, 5, axis=fr.n, smooth=False)


def spray(g, T, fr, u, v, size, c, kind='x', f=None):
    """A survivor's sprayed mark (bible 3.4: white or orange): an X (searched), or a ring with a stroke. Flat paint."""
    if T.lod > 0:
        return
    f = F['spray-orange'] if f is None else f
    w = size * .09
    if kind == 'x':
        for (p0, p1) in (((u - size / 2, v - size / 2), (u + size / 2, v + size / 2)),
                         ((u + size / 2, v - size / 2), (u - size / 2, v + size / 2))):
            dx, dy = p1[0] - p0[0], p1[1] - p0[1]
            L = math.hypot(dx, dy)
            px, py = -dy / L * w, dx / L * w
            g.face((fr.P(p0[0] + px, p0[1] + py, c), fr.P(p1[0] + px, p1[1] + py, c), fr.P(p1[0] - px, p1[1] - py, c),
                    fr.P(p0[0] - px, p0[1] - py, c)), f, fr.n)
            c += .0008
    else:
        n = 10
        for i in range(n):
            a0, a1 = 2 * math.pi * i / n, 2 * math.pi * (i + 1) / n
            r0, r1 = size / 2 - w, size / 2
            g.face((fr.P(u + r0 * math.cos(a0), v + r0 * math.sin(a0), c), fr.P(u + r1 * math.cos(a0), v + r1 * math.sin(a0), c),
                    fr.P(u + r1 * math.cos(a1), v + r1 * math.sin(a1), c), fr.P(u + r0 * math.cos(a1), v + r0 * math.sin(a1), c)),
                   f, fr.n)
        fquad(g, fr, u - w, v - size * .3, u + w, v + size * .3, c + .0008, f)


def boarded(g, T, fr, h, c, rnd, mark=None):
    """A window boarded up for the long night (bible 4.4): planks nailed across the opening inside its reveal, uneven in
    width, shade and length so the dark behind shows at their ends and between them; an arched head left as a dark void
    over the top plank; now and then a sprayed mark. LOD1: one board-coloured pane."""
    if not cut(T, h):
        fquad(g, fr, h.u0, h.v0, h.u1, h.top_v, c + .02, F['board-grey'])
        return
    back_fill(g, T, fr, h, c - h.depth + .02, F['interior'])
    face = c - .045
    y = h.v0 + .02
    top = h.v1 - .02
    k = 0
    high = h.v0 > 7.5      # (board 3: above the first floor the planks are wider and fewer; they read the same from the street)
    while y < top - .05:
        hh = min(top - y, rnd.uniform(.27, .34) if high else rnd.uniform(.17, .24))
        f = F[rnd.choice(('board-grey', 'board-grey', 'board-pale', 'timber-old'))]
        a = h.u0 + (rnd.uniform(0.0, .12) if rnd.random() < .3 else 0.0)
        b = h.u1 - (rnd.uniform(0.0, .14) if rnd.random() < .3 else 0.0)
        tilt = rnd.uniform(-.012, .012)
        fbox(g, T, fr, a + .005, b - .005, y + tilt, y + hh - .004 + tilt, face - .02, face, f, 0, skip=('-z',))
        y += hh + rnd.uniform(.012, .035)
        k += 1
    # two battens behind the planks' ends, the nails as dark dots on them
    if T.detail:
        for x in (h.u0 + .1, h.u1 - .1):
            fbox(g, T, fr, x - .04, x + .04, h.v0 + .05, top - .05, face - .035, face - .02, F['timber-old'], 0, skip=('-z',))
    if mark:
        spray(g, T, fr, (h.u0 + h.u1) / 2, (h.v0 + top) / 2, min(.5, (h.u1 - h.u0) * .5), face + .002, mark,
              F['spray-orange'] if rnd.random() < .7 else F['spray-white'])


# ---------------------------------------------------------------------------------------------------------------------------
# Small things
# ---------------------------------------------------------------------------------------------------------------------------
def downpipe(g, T, fr, u, v0, v1, c=.1, r=.055, f=None):
    """A cast-iron downpipe with its brackets and a hopper head (bible 5.1: the gutter's)."""
    f = F['iron-black'] if f is None else f
    sk.cylinder(g, fr.P(u, v0, c), fr.P(u, v1, c), r, f, T.small, caps=False)
    if T.detail:
        y = v0 + .9
        while y < v1 - .3:
            fbox(g, T, fr, u - r - .025, u + r + .025, y - .03, y + .03, 0.0, c + r * .5, f, 0, skip=('-z',))
            y += 2.7
        # the hopper: a flared box at the top
        prism(g, Frame(fr.P(u - .13, 0, 0), fr.u, fr.v), [(0.0, v1 - .28), (.26, v1 - .28), (.3, v1), (-.04, v1)], 0.0, c + .14, f,
              caps=(False, True))
        # the shoe at the foot
        fbox(g, T, fr, u - .07, u + .07, v0, v0 + .12, c - .02, c + .15, f, 0, skip=('-z', '-y'))   # .25 m proud at most


def gutter(g, T, fr, u0, u1, v, out=.12, f=None):
    """A cast-iron ogee gutter along the eaves at height v (its lip), standing `out` from the fascia (depth .05)."""
    f = F['iron-black'] if f is None else f
    prof = [(0.0, -.13), (.06, -.13), (.11, -.12), (.145, -.09), (.14, -.06), (.165, -.03), (.165, 0.0), (0.0, 0.0)]
    if T.lod > 0:
        prof = [(0.0, -.13), (.12, -.13), (.165, 0.0), (0.0, 0.0)]
    mould(g, T, fr, u0, u1, v, [(a * out / .165, b) for a, b in prof], f, ends=True, back=.05)


def chimney_pot(g, T, centre, f, h=.6, r=.12):
    """A clay chimney pot (a turned body with its rim) standing on `centre` (Unity space, +y up)."""
    # (board 3: four points, the dressed chunk's cap; pass 2's six. The rim's roll still reads on the skyline)
    prof = [(r * 1.06, 0.0), (r * .86, h * .6), (r * 1.15, h * .9), (r * .9, h)]
    if T.lod > 0:
        prof = [(r * 1.0, 0.0), (r * 1.05, h)]
    sk.lathe(g, prof, centre, f, T.small if T.lod == 0 else 4, cap1=False, smooth=T.lod == 0)
    # the flue: a dark disc just inside the rim
    if T.lod == 0:
        sk.lathe(g, [(r * .92, h - .02), (0.0, h - .02)], centre, F['batten'], T.small, smooth=False)


def lantern(g, T, centre, size, f_frame, f_glass, roof=True):
    """A small square lantern (porch or bracket lantern): four glazed sides between corner posts, a pyramid roof and a
    finial. centre: its middle (Unity space)."""
    x, y, z = centre
    s = size / 2
    hgt = size * 1.5
    # glazed body: a box of glow inside slim posts
    t = Geo()
    sk.box(t, -s * .8, s * .8, -hgt / 2, hgt / 2, -s * .8, s * .8, f_glass, None, 0)
    g.extend(t.transformed([1, 0, 0, x, 0, 1, 0, y, 0, 0, 1, z]))
    if T.lod > 0:
        return
    for (dx, dz) in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
        sk.box(g, x + dx * s - .015, x + dx * s + .015, y - hgt / 2, y + hgt / 2, z + dz * s - .015, z + dz * s + .015, f_frame, None, 0)
    # base and roof
    sk.box(g, x - s, x + s, y - hgt / 2 - .04, y - hgt / 2, z - s, z + s, f_frame, None, 0)
    if roof:
        top = y + hgt / 2
        sk.lathe(g, [(s * 1.77, 0.0), (s * 1.77, .03), (0.0, s * 1.1)], (x, top, z), f_frame, 4, smooth=False, phase=math.pi / 4)
        sk.lathe(g, [(.02, 0.0), (.035, .04), (0.0, .09)], (x, top + s * 1.1, z), f_frame, 4)


def rng(*k):
    """A seeded random generator from any keys (deterministic across runs)."""
    h = 2166136261
    for x in k:
        for ch in str(x):
            h = ((h ^ ord(ch)) * 16777619) & 0xFFFFFFFF
    return random.Random(h)


# ---------------------------------------------------------------------------------------------------------------------------
# Weathering sources for the bake (bible 4.4): rain streaks under sills, copings and cornice ends
# ---------------------------------------------------------------------------------------------------------------------------
class Streaks:
    """Rain streaks: each source a run along a wall (world points a, b on the wall's face, its outward normal) that darkens
    the wall below it, strongest just under it and fading over `length` metres. Applied after stagekit's Bake."""

    def __init__(self):
        self.src = []

    def add(self, a, b, n, length=1.4, k=.35):
        self.src.append((a, b, n, length, k))

    def add_under(self, fr, u0, u1, v, length=1.4, k=.35):
        self.add(fr.P(u0, v, 0.0), fr.P(u1, v, 0.0), fr.n, length, k)

    def transformed(self, m):
        import numpy as np
        A = np.array(m, dtype=np.float64).reshape(3, 4)
        R, t = A[:, :3], A[:, 3]
        out = Streaks()
        for (a, b, n, L, k) in self.src:
            a2 = tuple(R @ np.array(a) + t)
            b2 = tuple(R @ np.array(b) + t)
            n2 = tuple(R @ np.array(n))
            out.src.append((a2, b2, n2, L, k))
        return out

    def extend(self, other):
        self.src.extend(other.src)

    def apply(self, colour, P, N):
        """Darken colour (N, 4 uint8) at corners P (N, 3) with normals N (N, 3) in place."""
        import numpy as np
        if not self.src:
            return
        P = P.astype(np.float64)
        N = N.astype(np.float64)
        dark = np.zeros(len(P))
        walls = np.abs(N[:, 1]) < .35
        for (a, b, n, L, k) in self.src:
            a = np.array(a)
            b = np.array(b)
            n = np.array(n)
            d = b - a
            Ld = np.linalg.norm(d)
            if Ld < 1e-6:
                continue
            dirv = d / Ld
            rel = P - a
            along = rel @ dirv
            out = rel @ n
            below = a[1] - P[:, 1]
            m = walls & (np.abs(out) < .3) & (along > -.15) & (along < Ld + .15) & (below > -.02) & (below < L) & (N @ n > .5)
            if not m.any():
                continue
            fall = (1 - np.clip(below[m] / L, 0, 1)) ** 1.5
            edge = np.clip(np.minimum(along[m] + .15, Ld + .15 - along[m]) / .3, 0, 1)
            dark[m] = np.maximum(dark[m], k * fall * edge)
        rgb = colour[:, :3].astype(np.float64) * (1 - np.clip(dark, 0, .6))[:, None]
        colour[:, :3] = np.round(np.clip(rgb, 0, 255)).astype(np.uint8)


# ---------------------------------------------------------------------------------------------------------------------------
# Plan mouldings and corners
# ---------------------------------------------------------------------------------------------------------------------------
def plan_mould(g, T, pts, v, profile, f, outs=None, closed=False, caps=True):
    """A moulding run round a plan outline at height v (Unity: pts [(x, z)] on the wall line; `outs` the outline's outward
    normals per segment [(nx, nz)], or None: the walk's right-hand side seen from above with +z up the page); profile
    [(out, up)] from the face, bottom to top (monotone in up). The joints are mitred."""
    m = len(pts)
    ns = m if closed else m - 1
    if outs is None:
        outs = []
        for s_ in range(ns):
            (x0, z0), (x1, z1) = pts[s_], pts[(s_ + 1) % m]
            dx, dz = x1 - x0, z1 - z0
            L = math.hypot(dx, dz)
            outs.append((dz / L, -dx / L))
    mit, scl = [], []
    for i in range(m):
        a = outs[(i - 1) % ns] if (closed or i > 0) else outs[0]
        b = outs[i % ns] if (closed or i < m - 1) else outs[ns - 1]
        mx, mz = a[0] + b[0], a[1] + b[1]
        L = math.hypot(mx, mz) or 1.0
        mx, mz = mx / L, mz / L
        mit.append((mx, mz))
        scl.append(1.0 / max(.3, mx * b[0] + mz * b[1]))

    def P(i, a, b):
        x, z = pts[i % m]
        return (x + mit[i % m][0] * a * scl[i % m], v + b, z + mit[i % m][1] * a * scl[i % m])
    k = len(profile)
    for s_ in range(ns):
        i0, i1 = s_, s_ + 1
        nx, nz = outs[s_]
        for j in range(k - 1):
            (a0, b0), (a1, b1) = profile[j], profile[j + 1]
            da, db = a1 - a0, b1 - b0
            if abs(da) + abs(db) < 1e-7:
                continue
            g.face((P(i0, a0, b0), P(i1, a0, b0), P(i1, a1, b1), P(i0, a1, b1)), f, (nx * db, -da, nz * db))
    if caps and not closed:
        for (i, s_, sgn) in ((0, 0, -1), (m - 1, ns - 1, 1)):
            (x0, z0), (x1, z1) = pts[s_], pts[s_ + 1]
            dx, dz = x1 - x0, z1 - z0
            L = math.hypot(dx, dz)
            tn = (dx / L * sgn, 0.0, dz / L * sgn)
            for j in range(k - 1):
                (a0, b0), (a1, b1) = profile[j], profile[j + 1]
                if a0 < 1e-5 and a1 < 1e-5:
                    continue
                g.face((P(i, a0, b0), P(i, a1, b1), P(i, 0.0, b1), P(i, 0.0, b0)), f, tn)


def quoins(g, T, fr, u_edge, v0, v1, f, into=1, proud=.025, course=.3):
    """Quoins up a corner (bible 2.3): blocks alternately .45 and .25 m long on the face, `course` tall, 2.5 cm proud,
    running from u_edge into the face (`into` +1: toward +u). LOD0 only (shallow relief)."""
    if not T.detail:
        return
    k = 0
    v = v0
    while v < v1 - .05:
        top = min(v1, v + course)
        L = .45 if k % 2 == 0 else .25
        a, b = (u_edge, u_edge + L) if into > 0 else (u_edge - L, u_edge)
        inner = b if into > 0 else a
        ya, yb = v + .02, top - .02
        fquad(g, fr, a, ya, b, yb, proud, f)
        g.face((fr.P(a, yb, proud), fr.P(b, yb, proud), fr.P(b, top, 0.0), fr.P(a, top, 0.0)), f, fr.D(0, 1, 1))
        g.face((fr.P(a, v, 0.0), fr.P(b, v, 0.0), fr.P(b, ya, proud), fr.P(a, ya, proud)), f, fr.D(0, -1, 1))
        g.face((fr.P(inner, ya, 0.0), fr.P(inner, yb, 0.0), fr.P(inner, yb, proud), fr.P(inner, ya, proud)), f,
               fr.u if into > 0 else fr.mu)
        v = top
        k += 1


def arch_ring(g, T, fr, h, f, deep=.24, proud=.035, f2=None, key=None):
    """A ring of voussoirs over an arched opening: a band `deep` wide standing `proud` of the face; f2 alternates the
    voussoirs (polychrome); key: the keystone's finish (LOD0). LOD1 keeps it only round a kept opening."""
    if not cut(T, h):
        return
    cu = h.cuts(T)
    um = (h.u0 + h.u1) / 2
    inner, outer = [], []
    for a in cu:
        y = h.top(a)
        e = 1e-3
        ya, yb = h.top(max(h.u0, a - e)), h.top(min(h.u1, a + e))
        dx, dy = (min(h.u1, a + e) - max(h.u0, a - e)), yb - ya
        L = math.hypot(dx, dy) or 1
        nx, ny = -dy / L, dx / L
        inner.append((a, y))
        outer.append((a + nx * deep, y + ny * deep))
    for k in range(len(cu) - 1):
        (a0, b0), (a1, b1), (c1, d1), (c0, d0) = inner[k], inner[k + 1], outer[k + 1], outer[k]
        ff = f if (f2 is None or k % 2 == 0) else f2
        g.face((fr.P(a0, b0, proud), fr.P(a1, b1, proud), fr.P(c1, d1, proud), fr.P(c0, d0, proud)), ff, fr.n)
        g.face((fr.P(c0, d0, proud), fr.P(c1, d1, proud), fr.P(c1, d1, 0.0), fr.P(c0, d0, 0.0)), ff,
               fr.D(c0 + c1 - 2 * um, d0 + d1 - 2 * h.v1, 0))
        if proud > .035:
            # the ring's underside over the opening (pass 2: left off a thin ring; 3 cm facing down into the reveal)
            g.face((fr.P(a0, b0, proud), fr.P(a1, b1, proud), fr.P(a1, b1, 0.0), fr.P(a0, b0, 0.0)), ff,
                   fr.D(2 * um - a0 - a1, 2 * h.v1 - b0 - b1, 0))
    for k in (0, len(cu) - 1):
        (a0, b0), (c0, d0) = inner[k], outer[k]
        g.face((fr.P(a0, b0, proud), fr.P(c0, d0, proud), fr.P(c0, d0, 0.0), fr.P(a0, b0, 0.0)), f, (0, -1, 0))
    if key is not None and T.detail and h.rise > 0:
        t = h.top_v
        # (pass 2: a plain block; its chamfers were 40 of its 50 triangles)
        fbox(g, T, fr, um - .13, um + .13, t - .06, t + deep + .1, 0.0, proud + .04, key, 0, skip=('-z',))
