"""MERIDIAN kit (pass 2): the shared pieces of the expedition city's gates and caches (art bible 4.2, 5, 7, 8, 11.1).

tools/stages/meridian_gates_kit.py: board 3's gates-caches pass 2 kit (scratch-sources-2026-11-19/gates-caches/pass2/
meridian_kit.py), committed under its own name, unchanged but for its fallback path to stagekit, for meridian_landmarks.py
and meridian_lm_*.py. It is not the city kit of the same old name (tools/stages/meridian_kit.py, board 3's city_kit that
meridian_bank.py builds the streets from): the two share the finish table's name and some helpers, not their sets, so each
bank script imports its own.

Pass 2 (2026-11-19): every swept tube and section goes through this kit's own `sweep`, whose faces turn OUT of the section
(stagekit.sweep turns them inward, so its tubes are inside out under back-face culling; the owner's call: fix it for the Meridian
pieces now, leave the older stages alone). Lights are written in the game's terms: a point or a spot of the light field or a
practical slot (colour, gain, radius, cone), or an emissive face on the glow sub-mesh; no area lights.

A scratch draft of tools/stages/city_kit.py. Run inside Blender 5.x (numpy); geometry is stagekit's corner geometry (Geo, fin,
Tier, Bake), so a module script can write a bank with StageBank exactly as the stage scripts do.

Space: the game's (Unity: metres, +y up). The module frame (bible 11.1): a facade module stands on y = 0 with its collider face
in a plane z = const and faces -z, toward a viewer standing at -z; +x runs to the viewer's right, +y up. Lettering reads left to
right along +x. Every face is given its outward normal, and Geo.face winds it to agree (Unity's front faces).

Tiers (bible 10.2): LOD0 is stagekit's Fine tier on leaner counts (arcs 12, rounds 10, small 6; 0.75 m cells in the actor
band, 1.5 m above 6 m; bevels, detail and the tiniest parts on). LOD1 is the Low tier (arcs 6, rounds 6, small 4; 5 m cells;
no bevels, no detail).
"""
import math
import os
import sys

sys.dont_write_bytecode = True  # never leave caches beside a checkout's scripts
HERE = os.path.dirname(os.path.abspath(__file__))
_REPO_STAGES = os.path.join(HERE, '..', 'stages')  # (unused here: stagekit.py stands beside this file)
STAGES = HERE if os.path.exists(os.path.join(HERE, 'stagekit.py')) else os.environ.get('CHIP_STAGES', _REPO_STAGES)
if STAGES not in sys.path:
    sys.path.insert(0, STAGES)
import stagekit as sk  # noqa: E402
from stagekit import Geo, _add, _sub, _norm, _cross, _dot  # noqa: E402,F401

PI = math.pi


# ---------------------------------------------------------------------------------------------------------------------------
# Tiers
# ---------------------------------------------------------------------------------------------------------------------------
def tier(lod):
    """LOD0 (Fine on the bible's leaner counts) or LOD1 (stagekit's Low)."""
    if lod == 0:
        T = sk.Tier(0)
        T.arc, T.round, T.small, T.cell = 12, 10, 6, .75
    else:
        T = sk.Tier(2)
    T.lod = lod
    T.name = 'lod%d' % lod
    return T


def cell_at(T, y):
    """The vertex-colour cell at height y: 0.75 m in the actor band and shopfronts, 1.5 m above 6 m (LOD0); 5 m (LOD1)."""
    if T.lod:
        return 5.0
    return .75 if y < 6.0 else 1.5


# ---------------------------------------------------------------------------------------------------------------------------
# Finishes (bible 4.2, plus the gates' and the locker's own)
# ---------------------------------------------------------------------------------------------------------------------------
F = {}
GLOWS = {}


def _add_fin(name, hexc, detail=sk.DETAIL_NONE, smooth=.2, metal=0.0, dirt=1.0, moss=0.0, wear=0.0, param=0.0):
    F[name] = sk.fin('mer-' + name, hexc, detail, smooth=smooth, metal=metal, dirt=dirt, moss=moss, param=param, wear=wear)
    return F[name]


def glow(hexc, gain):
    """A glowing finish: its own sub-mesh, the emission gain in uv1.y (HDR; the bloom takes gains above 1)."""
    gain = round(max(.2, gain), 2)
    key = (hexc & 0xFFFFFF, gain)
    if key not in GLOWS:
        GLOWS[key] = sk.fin('mer-glow-%06x-%s' % key, hexc & 0xFFFFFF, sk.DETAIL_GLOW, glow=gain)
    return GLOWS[key]


def finishes():
    """The shared finish table (bible 4.2) and the gates' and locker's own finishes, by name in F."""
    if F:
        return F
    B, P, FL, T_, W, I, BR, CL = sk.DETAIL_BRICK, sk.DETAIL_PLASTER, sk.DETAIL_FLAG, sk.DETAIL_TILE, sk.DETAIL_WOOD, \
        sk.DETAIL_IRON, sk.DETAIL_BRASS, sk.DETAIL_CLOTH
    N = sk.DETAIL_NONE
    a = _add_fin
    # -- the city's common finishes (bible 4.2) --
    a('brick-stock', 0x8C4B36, B, .18, 0, 1.0, .25, .10)
    a('brick-blue', 0x4E4450, B, .32, 0, .8, .15, .05, 1.0)
    a('brick-buff', 0xB48A5E, B, .15, 0, 1.3, .10, .10, 1.05)
    a('brick-glazed', 0xE3D8BC, B, .55, 0, .6, 0, .05, 1.3)
    a('brick-soot', 0x4A3A34, B, .12, 0, .6, 0, .05)
    a('render-cream', 0xD8CAA8, P, .13, 0, 1.1, .20, .15)
    a('stucco-beige', 0xCBB88A, P, .10, 0, .6, 0, 0)
    a('stucco-joint', 0x8E7F5C, N, .10, 0, .6, 0, 0)
    a('stone-dress', 0xCFC5AF, FL, .20, 0, 1.0, .15, .25, .6)
    a('stone-civic', 0xD6CDB8, FL, .22, 0, .8, .10, .20, 1.0)
    a('granite', 0x7A7670, FL, .35, 0, .8, .10, .30, .5)
    a('setts', 0x5A5652, FL, .45, 0, .9, .30, .20, .22)
    a('paving', 0x6E6A64, FL, .40, 0, .9, .20, .20, .62)
    a('asphalt', 0x2E3038, N, .50, 0, .5, 0, 0)
    a('puddle', 0x23252C, N, .88, 0, 0, 0, 0)
    a('paint-line', 0xE6E0C8, N, .30, 0, .4, 0, .6)
    a('paint-yellow', 0xD8AC32, N, .30, 0, .4, 0, .6)
    a('slate', 0x4A5262, N, .50, 0, .6, .35, .05)
    a('lead', 0x6E767C, N, .45, .6, .5, 0, .10)
    a('copper', 0x5E9C88, BR, .35, .35, .4, 0, .20)
    a('tar-felt', 0x302E34, P, .20, 0, .5, .30, 0)
    a('iron-green', 0x24463A, N, .40, .25, .8, .10, .35)
    a('iron-black', 0x26262C, N, .45, .30, .7, 0, .30)
    a('iron-raw', 0x4A4E52, I, .35, .7, 1.2, 0, .20)
    a('galv', 0x9AA0A4, N, .50, .8, .6, 0, .10)
    a('chrome', 0xD8DCE0, N, .85, 1.0, .2, 0, .05)
    a('brass', 0xD2A840, BR, .70, 1.0, .3, 0, .30)
    a('enamel-cobalt', 0x1E3C96, N, .75, 0, .3, 0, .20)
    a('enamel-red', 0xB0232A, N, .75, 0, .3, 0, .20)
    a('enamel-cream', 0xEFE6CC, N, .75, 0, .3, 0, .20)
    a('enamel-green', 0x1F6A4A, N, .75, 0, .3, 0, .20)
    a('paint-green', 0x2C5A48, W, .45, 0, .8, .10, .30)
    a('paint-oxblood', 0x6A2626, W, .45, 0, .8, .10, .30)
    a('paint-maroon', 0x6A2626, N, .45, 0, .8, .05, .30)
    a('paint-navy', 0x233150, W, .45, 0, .8, .10, .30)
    a('paint-cream', 0xE4D9BC, W, .45, 0, .8, .10, .30)
    a('paint-black', 0x232126, W, .45, 0, .8, .10, .30)
    a('timber-old', 0x8A6E52, W, .15, 0, 1.0, .30, .10)
    a('concrete', 0x8A8882, P, .10, 0, 1.4, .20, .10)
    a('concrete-dark', 0x6E6D69, P, .10, 0, 1.4, .20, .10)
    a('glass-dark', 0x1A2230, N, .90, 0, .2, 0, 0)
    a('glass-frosted', 0xB8C8CC, N, .30, 0, .3, 0, 0)
    a('canvas', 0xB8A27A, CL, .10, 0, .8, .10, 0)
    a('rubber', 0x1E1E22, N, .25, 0, .6, 0, .05)
    a('steel-dark', 0x34383C, N, .45, .6, .6, 0, .25)
    a('void', 0x0C0C10, N, .10, 0, 0, 0, 0)
    # -- Level 0: Threshold Mutual (THE HUM) --
    a('veneer', 0xA9814F, W, .40, 0, .5, 0, .10)
    a('veneer-dark', 0x7A5A36, W, .40, 0, .5, 0, .10)
    a('kickplate', 0xB9BEC2, N, .65, .85, .4, 0, .25)
    a('alu', 0xB4BAC0, N, .55, .8, .4, 0, .10)
    a('alu-dark', 0x6E747A, N, .50, .7, .5, 0, .10)
    a('carpet', 0x8C7A44, CL, .05, 0, .9, .10, 0)
    a('carpet-damp', 0x5E5030, CL, .10, 0, .9, .20, 0)
    a('wallpaper', 0xC8B25A, P, .12, 0, .5, .05, 0)
    a('wallpaper-stripe', 0xB09A48, N, .12, 0, .5, .05, 0)
    a('skirting', 0xE6E0CC, W, .45, 0, .6, .05, .25)
    a('ceiling-tile', 0xD8D4C0, P, .08, 0, .6, 0, 0)
    a('ceiling-stain', 0x9C8A5C, P, .08, 0, .4, 0, 0)
    a('tbar', 0xE8E6DE, N, .50, .2, .4, 0, .10)
    a('glass-brown', 0x2E2418, N, .90, 0, .2, 0, 0)
    a('keypad', 0x3A3E44, N, .50, .5, .4, 0, .20)
    a('door-steel', 0x8A8A80, N, .45, .2, .6, 0, .25)
    a('stain', 0x9A8656, P, .10, 0, .3, 0, 0)
    # -- the Poolrooms: the Public Baths (THE DEEP END) --
    a('tile-pool', 0xE8F2F4, T_, .70, 0, .3, 0, 0, .15)
    a('tile-aqua', 0x3FB8B8, T_, .70, 0, .3, 0, 0, .15)
    a('tile-teal', 0x1F8C96, T_, .70, 0, .3, 0, 0, .15)
    a('tile-black', 0x1C2228, T_, .75, 0, .5, .05, 0, .15)
    a('tile-cream', 0xEADFC2, T_, .60, 0, .5, 0, .05, .15)
    a('tile-green', 0x2E6A4E, T_, .60, 0, .5, 0, .05, .15)
    a('mosaic-white', 0xE8F2F4, T_, .60, 0, .3, 0, 0, .04)
    a('mosaic-pale', 0x9FD0EE, T_, .60, 0, .3, 0, 0, .04)
    a('mosaic-blue', 0x2F7FD0, T_, .60, 0, .3, 0, 0, .04)
    a('mosaic-deep', 0x1C4A96, T_, .60, 0, .3, 0, 0, .04)
    a('mosaic-aqua', 0x3CC2C8, T_, .60, 0, .3, 0, 0, .04)
    a('mosaic-gold', 0xE8B84A, T_, .70, .4, .3, 0, 0, .04)
    a('water', 0x1A5C66, N, .92, 0, 0, 0, 0)
    a('coping', 0xF0F2EE, P, .55, 0, .5, .10, .10)
    a('float-pink', 0xE86A8C, N, .55, 0, .3, 0, 0)
    a('float-white', 0xF2EEE6, N, .55, 0, .3, 0, 0)
    # -- Ward Zero: the Decon Gate (THE SIREN) --
    a('cladding-white', 0xDCE6EA, N, .45, 0, .5, 0, .05)
    a('cladding-joint', 0x7C8A92, N, .30, 0, .4, 0, 0)
    a('cladding-teal', 0x1F9C9C, N, .50, 0, .4, 0, .05)
    a('steel-white', 0xE4E8EA, N, .55, .3, .4, 0, .15)
    a('pvc', 0xBFDCD6, N, .60, 0, .2, 0, 0)
    a('sheet', 0xD8E2E2, CL, .30, 0, .4, 0, 0)
    a('stencil-red', 0xB0232A, N, .30, 0, .3, 0, .4)
    a('stencil-black', 0x1E1E24, N, .30, 0, .3, 0, .4)
    a('decon-teal', 0x1F9C9C, N, .35, 0, .4, 0, .5)
    a('growth', 0x3A1E58, N, .55, 0, .2, 0, 0)
    a('growth-skin', 0x5A2C8A, N, .65, 0, .2, 0, 0)
    a('hose-yellow', 0xC8A030, N, .40, 0, .5, 0, .2)
    # pass 2: the growth's lichen and its webs, the sheeting's back, tapes and straps
    a('growth-dark', 0x24142E, N, .45, 0, .3, 0, 0)
    a('growth-membrane', 0x8A5AB8, N, .70, 0, .1, 0, 0)
    a('sheet-back', 0xAEB8B8, CL, .30, 0, .4, 0, 0)
    a('sheet-tape', 0xC9D3CF, N, .45, 0, .3, 0, .1)
    a('tape-red', 0xB0232A, N, .45, 0, .3, 0, .2)
    a('strap-yellow', 0xD0A028, N, .35, 0, .6, 0, .3)
    a('rope', 0x8E8670, CL, .15, 0, .8, 0, .1)
    # -- Central Station: the Station Road portico (THE 6:47) --
    a('cast-green', 0x2A4A3E, N, .45, .3, .8, .05, .35)
    a('cast-maroon', 0x5C2424, N, .45, .2, .8, .05, .35)
    a('board-black', 0x141418, N, .40, 0, .3, 0, 0)
    a('flap', 0x3A3A42, N, .45, 0, .2, 0, 0)
    a('glass-canopy', 0x2C3A48, N, .85, 0, .4, 0, 0)
    a('leather-brown', 0x5A3A26, N, .35, 0, .7, 0, .35)
    a('leather-tan', 0x9A6A3E, N, .35, 0, .7, 0, .35)
    a('case-green', 0x3A5A44, N, .40, 0, .6, 0, .3)
    a('case-maroon', 0x6A2E30, N, .40, 0, .6, 0, .3)
    a('rail', 0x8A8E92, N, .70, .9, .3, 0, .4)
    # -- City Ironworks: Furnace House No. 3 (THE CONVERTER) --
    a('rust', 0x7A3E22, I, .25, .3, .8, 0, .2)
    a('iron-heat', 0x3E3438, I, .30, .7, 1.0, 0, .2)
    a('iron-temper', 0x4A5A7A, I, .40, .8, .6, 0, .2)
    a('slag', 0x1E1A1C, N, .55, 0, .6, 0, 0)
    a('rubble', 0x7A5440, N, .15, 0, 1.0, .1, .2)
    a('interior', 0x1A1416, N, .20, 0, .5, 0, 0)
    a('cream-letter', 0xE8DEC2, N, .40, 0, .5, 0, .3)
    # -- the CD-647 locker --
    a('cd-olive', 0x5B6B3A, N, .45, .35, .7, 0, .35)
    a('cd-orange', 0xEE7420, N, .50, .3, .6, 0, .35)
    a('cd-cobalt', 0x1F3E8C, N, .75, .2, .5, 0, .30)
    a('cd-violet', 0x4A2A78, N, .65, .3, .5, 0, .30)
    a('cd-black', 0x262428, N, .55, .4, .5, 0, .30)
    a('cd-gold', 0xD2A840, BR, .75, 1.0, .3, 0, .30)
    a('cd-white', 0xECEBE2, N, .40, 0, .4, 0, .30)
    a('cd-lilac', 0xD8C6F2, N, .40, 0, .4, 0, .30)
    a('cd-steel', 0x30363A, N, .45, .6, .4, 0, .20)
    a('cd-tray', 0x22262A, N, .40, .3, .3, 0, 0)
    a('cd-cart', 0x2C3036, N, .55, .3, .3, 0, .2)
    return F


def hexmix(a, b, t):
    """Two sRGB hex colours mixed."""
    return (int((a >> 16 & 255) * (1 - t) + (b >> 16 & 255) * t) << 16) | \
        (int((a >> 8 & 255) * (1 - t) + (b >> 8 & 255) * t) << 8) | int((a & 255) * (1 - t) + (b & 255) * t)


# deterministic variety (the same hash as cybercity_kit.h01)
def _mix32(x):
    x &= 0xFFFFFFFF
    x ^= x >> 16
    x = (x * 0x7feb352d) & 0xFFFFFFFF
    x ^= x >> 15
    x = (x * 0x846ca68b) & 0xFFFFFFFF
    x ^= x >> 16
    return x


def h01(*k):
    h = 2166136261
    for v in k:
        if isinstance(v, str):
            for ch in v:
                h = ((h ^ ord(ch)) * 16777619) & 0xFFFFFFFF
        else:
            h = _mix32(h ^ _mix32(int(v) * 0x9E3779B9 + 0x85EBCA6B))
    return _mix32(h) / 4294967296.0


# ---------------------------------------------------------------------------------------------------------------------------
# Boxes and placed parts
# ---------------------------------------------------------------------------------------------------------------------------
def bx(g, T, x0, x1, y0, y1, z0, z1, f, bevel=.02, cell=None, skip=()):
    """An axis-aligned box: chamfered on LOD0 (none on LOD1, and none on small parts at LOD1); its faces cut to the vertex
    colours' cell when big (cell None: by height and size; 0: none)."""
    if cell is None:
        big = max(abs(x1 - x0), abs(y1 - y0), abs(z1 - z0))
        cell = cell_at(T, min(y0, y1)) if big > 1.6 else 0
    sk.box(g, x0, x1, y0, y1, z0, z1, f, T if (bevel and T.bevel) else None, bevel if T.bevel else 0.0, cell=cell, skip=skip)


def part(g, sub, q=(0.0, 0.0, 0.0, 1.0), t=(0.0, 0.0, 0.0)):
    """A sub-geometry turned by a Unity quaternion and moved to t."""
    g.extend(sub.transformed(sk.affine(q, t)))


def qy(deg):
    return sk.quat_axis(0, 1, 0, math.radians(deg))


def qx(deg):
    return sk.quat_axis(1, 0, 0, math.radians(deg))


def qz(deg):
    return sk.quat_axis(0, 0, 1, math.radians(deg))


def qmul(a, b):
    ax, ay, az, aw = a
    bx_, by, bz, bw = b
    return (aw * bx_ + ax * bw + ay * bz - az * by, aw * by - ax * bz + ay * bw + az * bx_,
            aw * bz + ax * by - ay * bx_ + az * bw, aw * bw - ax * bx_ - ay * by - az * bz)


def obx(g, T, c, half, q, f, bevel=.01):
    """A box turned by q about its centre c."""
    t = Geo()
    sk.box(t, -half[0], half[0], -half[1], half[1], -half[2], half[2], f, T if (bevel and T.bevel) else None,
           bevel if T.bevel else 0.0, cell=0)
    part(g, t, q, c)


def seg(g, T, a, b, w, d, f, up=(0, 1, 0)):
    """A bar of section w x d from a to b (a rectangular rod): w across (perpendicular to `up`), d toward `up`."""
    L = math.sqrt(_dot(_sub(b, a), _sub(b, a)))
    if L < 1e-6:
        return
    wv0 = _norm(_sub(b, a))
    if abs(_dot(wv0, _norm(up))) > .95:
        up = (1, 0, 0) if abs(wv0[0]) < .9 else (0, 0, 1)
    u, v, wv = sk.frame(a, b, up)
    hw, hd = w / 2, d / 2
    corners = [(-hw, -hd), (hw, -hd), (hw, hd), (-hw, hd)]
    P0 = [_add(_add(a, u, x), v, y) for x, y in corners]
    P1 = [_add(_add(b, u, x), v, y) for x, y in corners]
    sides = [(0, 1, (-v[0], -v[1], -v[2])), (1, 2, u), (2, 3, v), (3, 0, (-u[0], -u[1], -u[2]))]
    for i, j, n in sides:
        g.face((P0[i], P0[j], P1[j], P1[i]), f, n)
    g.face(P0, f, (-wv[0], -wv[1], -wv[2]))
    g.face(P1, f, wv)


def _lerp0(a, k):
    return (a[0] * k, a[1] * k, a[2] * k)


def rod(g, a, b, r, f, sides, caps=True, r1=None):
    if _dot(_sub(b, a), _sub(b, a)) < 1e-10:
        return
    sk.cylinder(g, a, b, r, f, sides, caps=caps, r1=r1)


def sweep(g, section, path, f, closed=False, up=None, caps=True, smooth_u=True, scale=None):
    """A section [(x, y)] swept along a path (Unity space), as stagekit.sweep (rotation-minimising frames from `up`; `scale` per
    point scales the section), but with every side's normal turned OUT of the section whichever way it winds. stagekit.sweep
    takes the sign the wrong way round: a counter-clockwise section (every circle_section and rect_section) comes out with
    its normals, and so its winding, pointing into the tube (inside out under back-face culling). Pass 2 routes every Meridian
    tube through here; the older stages keep stagekit's."""
    m = len(path)
    if m < 2:
        return
    T_ = []
    for i in range(m):
        a = path[(i - 1) % m] if (closed or i > 0) else path[i]
        b = path[(i + 1) % m] if (closed or i < m - 1) else path[i]
        T_.append(_norm(_sub(b, a)))
    ref = up if up is not None else ((0, 1, 0) if abs(T_[0][1]) < .95 else (1, 0, 0))
    u = _norm(_cross(ref, T_[0]))
    v = _cross(T_[0], u)
    frames = [(u, v)]
    for i in range(1, m):
        v1 = _sub(path[i], path[i - 1])
        c1 = _dot(v1, v1)
        if c1 < 1e-14:
            frames.append(frames[-1])
            continue
        uL = _add(u, v1, -2.0 / c1 * _dot(v1, u))
        tL = _add(T_[i - 1], v1, -2.0 / c1 * _dot(v1, T_[i - 1]))
        v2 = _sub(T_[i], tL)
        c2 = _dot(v2, v2)
        u = _norm(_add(uL, v2, -2.0 / c2 * _dot(v2, uL)) if c2 > 1e-14 else uL)
        v = _cross(T_[i], u)
        frames.append((u, v))
    k = len(section)
    area = sum(section[j][0] * section[(j + 1) % k][1] - section[(j + 1) % k][0] * section[j][1] for j in range(k))
    sgn = 1.0 if area > 0 else -1.0       # (dy, -dx) of a counter-clockwise outline points out of it
    sn = []
    for j in range(k):
        a, b = section[(j - 1) % k], section[(j + 1) % k]
        d = (b[0] - a[0], b[1] - a[1])
        ln = math.hypot(d[0], d[1]) or 1.0
        sn.append((sgn * d[1] / ln, -sgn * d[0] / ln))
    rings, nrings = [], []
    for i in range(m):
        uu, vv = frames[i]
        s_ = scale[i] if scale is not None else 1.0
        rings.append([_add(_add(path[i], uu, x * s_), vv, y * s_) for (x, y) in section])
        nrings.append([_norm(_add(_lerp0(uu, nx), vv, ny)) for (nx, ny) in sn])
    count = m if closed else m - 1
    for i in range(count):
        i1 = (i + 1) % m
        for j in range(k):
            j1 = (j + 1) % k
            pts = (rings[i][j], rings[i][j1], rings[i1][j1], rings[i1][j])
            nn = (nrings[i][j], nrings[i][j1], nrings[i1][j1], nrings[i1][j])
            if smooth_u:
                g.face(pts, f, normals=nn)
            else:
                ex, ey = section[j1][0] - section[j][0], section[j1][1] - section[j][1]
                el = math.hypot(ex, ey) or 1.0
                nx, ny = sgn * ey / el, -sgn * ex / el
                u0, v0 = frames[i]
                u1, v1_ = frames[i1]
                g.face(pts, f, normal=_norm(_add(_add(_lerp0(u0, nx), v0, ny), _add(_lerp0(u1, nx), v1_, ny))))
    if caps and not closed:
        if (scale is None or scale[0] > 1e-4):
            g.face(rings[0], f, normal=(-T_[0][0], -T_[0][1], -T_[0][2]))
        if (scale is None or scale[-1] > 1e-4):
            g.face(rings[-1], f, normal=T_[-1])


def tube_path(g, pts, r, f, sides, caps=True, radii=None, up=None):
    """A round tube along a polyline (outward faces; `radii` per point tapers it)."""
    if len(pts) < 2:
        return
    sc = None if radii is None else [max(1e-4, x) / r for x in radii]
    sweep(g, sk.circle_section(r, sides, math.pi / sides), pts, f, caps=caps, scale=sc, up=up)


def ellipsoid(g, c, radii, f, sides, rings=4, axis=(0, 1, 0), top=1.0, bottom=-1.0):
    """A closed ellipsoid (or a cut of one) about `axis` through c: radii (across, along); its normals smooth and outward."""
    ra, rl = radii
    prof = []
    for i in range(rings + 1):
        t = bottom + (top - bottom) * i / rings
        prof.append((ra * math.sqrt(max(0.0, 1 - t * t)), rl * t))
    sk.lathe(g, prof, c, f, sides, axis=axis, cap0=bottom > -1 + 1e-6, cap1=top < 1 - 1e-6)


def quad(g, a, b, c, d, f, n):
    g.face((a, b, c, d), f, n)


def flat_poly(g, pts2, y, f, up=True):
    """A flat convex polygon on the ground plane y (points (x, z))."""
    g.face([(p[0], y, p[1]) for p in pts2], f, (0, 1 if up else -1, 0))


def ground(g, T, x0, x1, z0, z1, y, f, cell=None):
    """A flat floor field, cut to the vertex colours' cell."""
    c = cell_at(T, 0) if cell is None else (cell or 1e9)
    sk.grid_face(g, (x0, y, z0), (x1 - x0, 0, 0), (0, 0, z1 - z0), 0, 0, f, (0, 1, 0), c)


def wall_rect(g, T, x0, x1, y0, y1, z, f, n=-1, cell=None):
    """A flat rectangle in the plane z facing -z (n = -1) or +z (n = 1)."""
    c = cell_at(T, y0) if cell is None else (cell or 1e9)
    sk.grid_face(g, (x0, y0, z), (x1 - x0, 0, 0), (0, y1 - y0, 0), 0, 0, f, (0, 0, n), c)


def side_rect(g, T, z0, z1, y0, y1, x, f, n=1, cell=None):
    """A flat rectangle in the plane x facing +x (n = 1) or -x."""
    c = cell_at(T, y0) if cell is None else (cell or 1e9)
    sk.grid_face(g, (x, y0, z0), (0, 0, z1 - z0), (0, y1 - y0, 0), 0, 0, f, (n, 0, 0), c)


# ---------------------------------------------------------------------------------------------------------------------------
# Walls with openings (rectangular and arched heads), their reveals
# ---------------------------------------------------------------------------------------------------------------------------
class Hole:
    """An opening x0..x1, y0..y1 to the springing, with an arched head of `rise` over y1 (0 flat; half the width: round)."""

    def __init__(self, x0, x1, y0, y1, rise=0.0):
        self.x0, self.x1, self.y0, self.y1, self.rise = x0, x1, y0, y1, rise

    def top(self, x):
        if self.rise <= 0:
            return self.y1
        L = (self.x1 - self.x0) / 2
        d = min(x - self.x0, self.x1 - x)
        return self.y1 + sk.arch_height(L, self.rise, max(0.0, d))

    @property
    def apex(self):
        return self.y1 + self.rise


def wall(g, T, z, x0, x1, y0, y1, f, holes=(), n=-1, cell=None):
    """A flat wall in the plane z facing -z (n = -1) or +z, x0..x1 by y0..y1, with the openings cut out. It is laid in
    vertical strips at every opening's edge (and along each arched head), so every piece is a convex quad."""
    xs = {x0, x1}
    for h in holes:
        for x in (h.x0, h.x1):
            if x0 < x < x1:
                xs.add(x)
        if h.rise > 0:
            k = max(2, T.arc)
            for i in range(1, k):
                x = h.x0 + (h.x1 - h.x0) * i / k
                if x0 < x < x1:
                    xs.add(x)
    xs = sorted(xs)
    for xa, xb in zip(xs[:-1], xs[1:]):
        if xb - xa < 1e-5:
            continue
        cuts = []
        for h in holes:
            if h.x0 - 1e-6 <= xa and xb <= h.x1 + 1e-6 and h.y0 < y1 and h.apex > y0:
                cuts.append((max(y0, h.y0), min(y1, h.top(xa)), min(y1, h.top(xb))))
        cuts.sort()
        ba = bb = y0
        for (hb, ta, tb) in cuts:
            if hb > min(ba, bb) + 1e-5:
                _strip(g, T, z, xa, xb, ba, bb, hb, hb, f, n, cell)
            ba, bb = max(ba, ta), max(bb, tb)
        if y1 > min(ba, bb) + 1e-5:
            _strip(g, T, z, xa, xb, ba, bb, y1, y1, f, n, cell)


def _strip(g, T, z, xa, xb, ba, bb, ta, tb, f, n, cell):
    """A trapezoid (xa, ba)-(xb, bb)-(xb, tb)-(xa, ta) in the plane z, cut into cells."""
    if ta - ba < 1e-5 and tb - bb < 1e-5:
        return
    c = cell_at(T, min(ba, bb)) if cell is None else cell
    nu = max(1, int(math.ceil((xb - xa) / c))) if c else 1
    nv = max(1, int(math.ceil(max(ta - ba, tb - bb) / c))) if c else 1
    for i in range(nu):
        s0, s1 = i / nu, (i + 1) / nu
        X0, X1 = xa + (xb - xa) * s0, xa + (xb - xa) * s1
        B0, B1 = ba + (bb - ba) * s0, ba + (bb - ba) * s1
        T0, T1 = ta + (tb - ta) * s0, ta + (tb - ta) * s1
        for j in range(nv):
            t0, t1 = j / nv, (j + 1) / nv
            p = ((X0, B0 + (T0 - B0) * t0, z), (X1, B1 + (T1 - B1) * t0, z), (X1, B1 + (T1 - B1) * t1, z), (X0, B0 + (T0 - B0) * t1, z))
            g.face(p, f, (0, 0, n))


def reveal(g, T, h, z0, z1, f, sill=True, head=True, jambs=True, fhead=None):
    """An opening's reveal from the wall face z0 back to z1 (z1 > z0 for a facade facing -z): the jambs, the sill's top and the
    head's soffit (flat, or in strips along the arch)."""
    c = cell_at(T, h.y0)
    if jambs:
        sk.grid_face(g, (h.x0, h.y0, z0), (0, 0, z1 - z0), (0, h.y1 - h.y0, 0), 0, 0, f, (1, 0, 0), c)
        sk.grid_face(g, (h.x1, h.y0, z0), (0, 0, z1 - z0), (0, h.y1 - h.y0, 0), 0, 0, f, (-1, 0, 0), c)
    if sill and h.y0 > 1e-4:
        g.face(((h.x0, h.y0, z0), (h.x1, h.y0, z0), (h.x1, h.y0, z1), (h.x0, h.y0, z1)), f, (0, 1, 0))
    if head:
        fh = f if fhead is None else fhead
        if h.rise <= 0:
            g.face(((h.x0, h.y1, z0), (h.x1, h.y1, z0), (h.x1, h.y1, z1), (h.x0, h.y1, z1)), fh, (0, -1, 0))
        else:
            k = max(2, T.arc)
            cx = (h.x0 + h.x1) / 2
            for i in range(k):
                xa = h.x0 + (h.x1 - h.x0) * i / k
                xb = h.x0 + (h.x1 - h.x0) * (i + 1) / k
                ya, yb = h.top(xa), h.top(xb)
                mid = ((xa + xb) / 2, (ya + yb) / 2)
                nrm = _norm((cx - mid[0], h.y1 - mid[1], 0))
                g.face(((xa, ya, z0), (xb, yb, z0), (xb, yb, z1), (xa, ya, z1)), fh, nrm)


# ---------------------------------------------------------------------------------------------------------------------------
# Mouldings: a profile run along a path in plan, mitred at its corners
# ---------------------------------------------------------------------------------------------------------------------------
def mould(g, T, prof, path, f, y=0.0, caps=(True, True), smooth=False, closed=False, cell=None):
    """A moulding: `prof` [(out, up)] from the wall at its foot (out 0) out and up and back to the wall at its top (out 0),
    counter-clockwise in (out, up); run along `path` [(x, z)] (the wall's line in plan, walked with the wall on the left so
    'out' is to the right: for a facade facing -z, walk +x). y lifts the profile. Mitred at the path's corners; capped at its
    ends (the cap is cut in trapezoids, so its `up` must rise along the profile)."""
    m = len(path)
    if m < 2:
        return
    outs = []
    for i in range(m - 1 + (1 if closed else 0)):
        a, b = path[i], path[(i + 1) % m]
        d = _norm((b[0] - a[0], 0, b[1] - a[1]))
        outs.append((d[2], -d[0]))   # right of the walk, in plan
    mit = []
    for i in range(m):
        if closed:
            o0, o1 = outs[(i - 1) % len(outs)], outs[i % len(outs)]
        else:
            o0 = outs[max(0, i - 1)]
            o1 = outs[min(len(outs) - 1, i)]
        sx, sz = o0[0] + o1[0], o0[1] + o1[1]
        dd = 1.0 + o0[0] * o1[0] + o0[1] * o1[1]
        mit.append((sx / dd, sz / dd) if dd > 1e-6 else o1)
    k = len(prof)
    # 2D normals of the profile's edges (CCW in (out, up): the outward normal of edge (do, du) is (du, -do))
    en = []
    for j in range(k - 1):
        do, du = prof[j + 1][0] - prof[j][0], prof[j + 1][1] - prof[j][1]
        l = math.hypot(do, du)
        en.append((du / l, -do / l) if l > 1e-9 else (1.0, 0.0))
    vn = []
    for j in range(k):
        if smooth:
            a_ = en[max(0, j - 1)]
            b_ = en[min(k - 2, j)]
            vn.append(_norm((a_[0] + b_[0], a_[1] + b_[1], 0)))
        else:
            vn.append(None)

    def P(i, j):
        px, pz = path[i % m]
        mx, mz = mit[i % m]
        o, u = prof[j]
        return (px + mx * o, y + u, pz + mz * o)
    segs = m if closed else m - 1
    for i in range(segs):
        ox, oz = outs[i]
        a, b = path[i], path[(i + 1) % m]
        L = math.hypot(b[0] - a[0], b[1] - a[1])
        c = cell_at(T, y) if cell is None else cell
        n_along = max(1, int(math.ceil(L / c))) if c else 1
        for j in range(k - 1):
            p0, p1, p2, p3 = P(i, j), P(i + 1, j), P(i + 1, j + 1), P(i, j + 1)
            eo, eu = en[j]
            nf = (ox * eo, eu, oz * eo)
            for s in range(n_along):
                t0, t1 = s / n_along, (s + 1) / n_along
                q0, q1 = _lerp(p0, p1, t0), _lerp(p0, p1, t1)
                q3, q2 = _lerp(p3, p2, t0), _lerp(p3, p2, t1)
                if smooth:
                    na = (ox * vn[j][0], vn[j][1], oz * vn[j][0])
                    nb = (ox * vn[j + 1][0], vn[j + 1][1], oz * vn[j + 1][0])
                    g.face((q0, q1, q2, q3), f, normals=(na, na, nb, nb))
                else:
                    g.face((q0, q1, q2, q3), f, nf)
    if not closed:
        for end, ii in ((0, 0), (1, m - 1)):
            if not caps[end]:
                continue
            a, b = path[0], path[1]
            if end:
                a, b = path[m - 2], path[m - 1]
            d = _norm((b[0] - a[0], 0, b[1] - a[1]))
            nrm = (-d[0], 0, -d[2]) if end == 0 else d
            for j in range(k - 1):
                p0, p1 = P(ii, j), P(ii, j + 1)
                w0 = (path[ii][0], p0[1], path[ii][1])
                w1 = (path[ii][0], p1[1], path[ii][1])
                g.face((w0, p0, p1, w1), f, nrm)


def _lerp(a, b, t):
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t)


# profiles (out, up), counter-clockwise from the wall at the foot to the wall at the top
def prof_cornice(s=1.0):
    """A classical cornice: bed mould, a corona with its drip's lip, a cyma on top (simplified to rise monotonically)."""
    return [(0, 0), (.06 * s, 0), (.06 * s, .05 * s), (.14 * s, .09 * s), (.30 * s, .12 * s), (.40 * s, .14 * s),
            (.40 * s, .30 * s), (.36 * s, .32 * s), (.30 * s, .38 * s), (.20 * s, .44 * s), (0, .46 * s)]


def prof_string(h=.12, o=.06):
    return [(0, 0), (o * .6, 0), (o, h * .3), (o, h * .8), (o * .7, h), (0, h)]


def prof_coping(h=.22, o=.05, w=.0):
    """A parapet coping: overhangs by o, with a drip and a weathered (sloped) top."""
    return [(0, 0), (o, 0), (o, h * .55), (o * .6, h), (0, h * .92)]


def prof_plinth(h=.6, o=.05):
    return [(0, 0), (o, 0), (o, h * .82), (o * .3, h), (0, h)]


def prof_deco_steps(h=.6, o=.24, n=3):
    pts = [(0, 0)]
    for i in range(n):
        oo = o * (n - i) / n
        pts += [(oo, h * i / n), (oo, h * (i + 1) / n)]
    pts += [(0, h)]
    # drop the duplicate first point after (0, 0)
    out = [pts[0]]
    for p in pts[1:]:
        if abs(p[0] - out[-1][0]) > 1e-9 or abs(p[1] - out[-1][1]) > 1e-9:
            out.append(p)
    return out


# ---------------------------------------------------------------------------------------------------------------------------
# Lettering: a 5 x 7 face (painted, stencilled, enamel, LED dots) and a stroke font (cast, neon, Deco)
# ---------------------------------------------------------------------------------------------------------------------------
PIX = {
    'A': '.###.,#...#,#...#,#####,#...#,#...#,#...#', 'B': '####.,#...#,#...#,####.,#...#,#...#,####.',
    'C': '.####,#....,#....,#....,#....,#....,.####', 'D': '####.,#...#,#...#,#...#,#...#,#...#,####.',
    'E': '#####,#....,#....,####.,#....,#....,#####', 'F': '#####,#....,#....,####.,#....,#....,#....',
    'G': '.####,#....,#....,#..##,#...#,#...#,.####', 'H': '#...#,#...#,#...#,#####,#...#,#...#,#...#',
    'I': '###,.#.,.#.,.#.,.#.,.#.,###', 'J': '..###,...#.,...#.,...#.,...#.,#..#.,.##..',
    'K': '#...#,#..#.,#.#..,##...,#.#..,#..#.,#...#', 'L': '#....,#....,#....,#....,#....,#....,#####',
    'M': '#...#,##.##,#.#.#,#.#.#,#...#,#...#,#...#', 'N': '#...#,##..#,#.#.#,#..##,#...#,#...#,#...#',
    'O': '.###.,#...#,#...#,#...#,#...#,#...#,.###.', 'P': '####.,#...#,#...#,####.,#....,#....,#....',
    'Q': '.###.,#...#,#...#,#...#,#.#.#,#..#.,.##.#', 'R': '####.,#...#,#...#,####.,#.#..,#..#.,#...#',
    'S': '.####,#....,#....,.###.,....#,....#,####.', 'T': '#####,..#..,..#..,..#..,..#..,..#..,..#..',
    'U': '#...#,#...#,#...#,#...#,#...#,#...#,.###.', 'V': '#...#,#...#,#...#,#...#,.#.#.,.#.#.,..#..',
    'W': '#...#,#...#,#...#,#.#.#,#.#.#,##.##,#...#', 'X': '#...#,.#.#.,..#..,..#..,..#..,.#.#.,#...#',
    'Y': '#...#,#...#,.#.#.,..#..,..#..,..#..,..#..', 'Z': '#####,....#,...#.,..#..,.#...,#....,#####',
    '0': '.###.,#...#,#..##,#.#.#,##..#,#...#,.###.', '1': '..#..,.##..,..#..,..#..,..#..,..#..,.###.',
    '2': '.###.,#...#,....#,...#.,..#..,.#...,#####', '3': '####.,....#,....#,.###.,....#,....#,####.',
    '4': '...#.,..##.,.#.#.,#..#.,#####,...#.,...#.', '5': '#####,#....,####.,....#,....#,#...#,.###.',
    '6': '.###.,#....,#....,####.,#...#,#...#,.###.', '7': '#####,....#,...#.,..#..,.#...,.#...,.#...',
    '8': '.###.,#...#,#...#,.###.,#...#,#...#,.###.', '9': '.###.,#...#,#...#,.####,....#,....#,.###.',
    '$': '..#..,.####,#.#..,.###.,..#.#,####.,..#..', ':': '.,#,.,.,.,#,.', '.': '.,.,.,.,.,.,#',
    '·': '.,.,.,#,.,.,.', '-': '...,...,...,###,...,...,...', '/': '....#,...#.,...#.,..#..,.#...,.#...,#....',
    "'": '#,#,.,.,.,.,.', '!': '#,#,#,#,#,.,#', '&': '.##..,#..#.,#.#..,.#...,#.#.#,#..#.,.##.#',
    '+': '.....,..#..,..#..,#####,..#..,..#..,.....', '>': '#....,.#...,..#..,...#.,..#..,.#...,#....',
    '<': '....#,...#.,..#..,.#...,..#..,...#.,....#', ' ': '...,...,...,...,...,...,...', '#': '.#.#.,#####,.#.#.,.#.#.,.#.#.,#####,.#.#.',
}


def pix_width(s, px):
    w = 0.0
    for ch in s:
        w += (len(PIX.get(ch, PIX[' ']).split(',')[0]) + 1) * px
    return w - px


def _runs(row):
    out, i, n = [], 0, len(row)
    while i < n:
        if row[i] != '#':
            i += 1
            continue
        j = i
        while j < n and row[j] == '#':
            j += 1
        out.append((i, j))
        i = j
    return out


def text_pix(g, s, x, y, z, px, f, style='raised', depth=.012, align='c', T=None, gap=.0):
    """5 x 7 lettering on a face facing -z, its foot at y, starting (align 'l'), centred ('c') or ending ('r') at x. style:
    'raised' (strokes `depth` proud), 'flat' (faces lying `depth` off the face), 'stencil' (each row its own bar, a bridge
    between rows), 'dots' (each cell its own square: an LED matrix). Runs that repeat down the rows merge into one face
    ('raised', 'flat'). Returns the text's width."""
    w = pix_width(s, px)
    cx = x - (w / 2 if align == 'c' else (w if align == 'r' else 0))
    zz = z - depth
    for ch in s:
        rows = PIX.get(ch, PIX[' ']).split(',')
        cw = len(rows[0])
        if style == 'dots':
            for r, row in enumerate(rows):
                yy = y + (6 - r) * px
                for i, c in enumerate(row):
                    if c == '#':
                        m = px * .12
                        a0, a1 = cx + i * px + m, cx + (i + 1) * px - m
                        g.face(((a0, yy + m, zz), (a1, yy + m, zz), (a1, yy + px - m, zz), (a0, yy + px - m, zz)), f, (0, 0, -1))
            cx += (cw + 1) * px
            continue
        rects = []  # (i, j, r_top, r_bottom)
        open_ = {}
        for r, row in enumerate(rows):
            runs = _runs(row)
            nxt = {}
            for run in runs:
                if style != 'stencil' and run in open_:
                    rr = open_[run]
                    rects[rr][3] = r
                    nxt[run] = rr
                else:
                    rects.append([run[0], run[1], r, r])
                    nxt[run] = len(rects) - 1
            open_ = nxt
        for (i, j, r0, r1) in rects:
            a0, a1 = cx + i * px, cx + j * px
            b0, b1 = y + (6 - r1) * px, y + (7 - r0) * px
            if style == 'stencil':
                b0 += px * .09
                b1 -= px * .09
            if style == 'raised' and T is not None and T.detail:
                bx(g, T, a0, a1, b0, b1, zz, z, f, bevel=0, cell=0, skip=('+z',))
            else:
                g.face(((a0, b0, zz), (a1, b0, zz), (a1, b1, zz), (a0, b1, zz)), f, (0, 0, -1))
        cx += (cw + 1) * px
    return w


# the stroke font: polylines on a 4 x 6 grid (x 0..4, y 0..6)
STROKES = {
    'A': [[(0, 0), (0, 4.4), (1.4, 6), (2.6, 6), (4, 4.4), (4, 0)], [(0, 2.9), (4, 2.9)]],
    'B': [[(0, 0), (0, 6), (3, 6), (4, 5.1), (4, 4.1), (3, 3.2), (0, 3.2)], [(3, 3.2), (4, 2.3), (4, .9), (3, 0), (0, 0)]],
    'C': [[(4, 5), (3, 6), (1, 6), (0, 5), (0, 1), (1, 0), (3, 0), (4, 1)]],
    'D': [[(0, 0), (0, 6), (2.6, 6), (4, 4.6), (4, 1.4), (2.6, 0), (0, 0)]],
    'E': [[(4, 6), (0, 6), (0, 0), (4, 0)], [(0, 3.1), (3, 3.1)]],
    'F': [[(4, 6), (0, 6), (0, 0)], [(0, 3.1), (3, 3.1)]],
    'G': [[(4, 5), (3, 6), (1, 6), (0, 5), (0, 1), (1, 0), (3, 0), (4, 1), (4, 2.8), (2.2, 2.8)]],
    'H': [[(0, 0), (0, 6)], [(4, 0), (4, 6)], [(0, 3.1), (4, 3.1)]],
    'I': [[(2, 0), (2, 6)], [(1, 6), (3, 6)], [(1, 0), (3, 0)]],
    'J': [[(4, 6), (4, 1), (3, 0), (1, 0), (0, 1)]],
    'K': [[(0, 0), (0, 6)], [(4, 6), (0, 2.3)], [(1.3, 3.5), (4, 0)]],
    'L': [[(0, 6), (0, 0), (4, 0)]],
    'M': [[(0, 0), (0, 6), (2, 2.6), (4, 6), (4, 0)]],
    'N': [[(0, 0), (0, 6), (4, 0), (4, 6)]],
    'O': [[(1, 0), (0, 1), (0, 5), (1, 6), (3, 6), (4, 5), (4, 1), (3, 0), (1, 0)]],
    'P': [[(0, 0), (0, 6), (3, 6), (4, 5), (4, 3.8), (3, 2.8), (0, 2.8)]],
    'Q': [[(1, 0), (0, 1), (0, 5), (1, 6), (3, 6), (4, 5), (4, 1), (3, 0), (1, 0)], [(2.4, 1.6), (4.2, -.4)]],
    'R': [[(0, 0), (0, 6), (3, 6), (4, 5), (4, 3.8), (3, 2.8), (0, 2.8)], [(2, 2.8), (4, 0)]],
    'S': [[(4, 5), (3, 6), (1, 6), (0, 5), (0, 4), (1, 3.1), (3, 3.1), (4, 2.1), (4, 1), (3, 0), (1, 0), (0, 1)]],
    'T': [[(0, 6), (4, 6)], [(2, 6), (2, 0)]],
    'U': [[(0, 6), (0, 1), (1, 0), (3, 0), (4, 1), (4, 6)]],
    'V': [[(0, 6), (2, 0), (4, 6)]],
    'W': [[(0, 6), (.8, 0), (2, 4), (3.2, 0), (4, 6)]],
    'X': [[(0, 6), (4, 0)], [(0, 0), (4, 6)]],
    'Y': [[(0, 6), (2, 3), (4, 6)], [(2, 3), (2, 0)]],
    'Z': [[(0, 6), (4, 6), (0, 0), (4, 0)]],
    '0': [[(1, 0), (0, 1), (0, 5), (1, 6), (3, 6), (4, 5), (4, 1), (3, 0), (1, 0)]],
    '1': [[(.9, 4.9), (2, 6), (2, 0)], [(.9, 0), (3.1, 0)]],
    '2': [[(0, 5), (1, 6), (3, 6), (4, 5), (4, 4), (0, 0), (4, 0)]],
    '3': [[(0, 5), (1, 6), (3, 6), (4, 5), (4, 4), (3, 3.1), (1.4, 3.1)], [(3, 3.1), (4, 2.1), (4, 1), (3, 0), (1, 0), (0, 1)]],
    '4': [[(3, 0), (3, 6), (0, 2), (4, 2)]],
    '5': [[(4, 6), (0, 6), (0, 3.4), (3, 3.4), (4, 2.4), (4, 1), (3, 0), (1, 0), (0, 1)]],
    '6': [[(4, 5), (3, 6), (1, 6), (0, 5), (0, 1), (1, 0), (3, 0), (4, 1), (4, 2.4), (3, 3.4), (0, 3.4)]],
    '7': [[(0, 6), (4, 6), (1.4, 0)]],
    '8': [[(1, 3.1), (0, 4.1), (0, 5), (1, 6), (3, 6), (4, 5), (4, 4.1), (3, 3.1), (1, 3.1), (0, 2.1), (0, 1), (1, 0), (3, 0),
           (4, 1), (4, 2.1), (3, 3.1)]],
    '9': [[(0, 1), (1, 0), (3, 0), (4, 1), (4, 5), (3, 6), (1, 6), (0, 5), (0, 3.6), (1, 2.6), (4, 2.6)]],
    '.': [[(1.6, 0), (2.4, 0)]], '·': [[(1.6, 3), (2.4, 3)]], '-': [[(.6, 3), (3.4, 3)]],
    ':': [[(2, .8), (2, 1.5)], [(2, 4.5), (2, 5.2)]], '/': [[(0, 0), (4, 6)]],
    '$': [[(4, 5), (3, 5.6), (1, 5.6), (0, 4.8), (0, 4), (1, 3.1), (3, 3.1), (4, 2.2), (4, 1.2), (3, .4), (1, .4), (0, 1)], [(2, -.3), (2, 6.3)]],
    'o': [[(1, 0), (0, 1), (0, 3), (1, 4), (3, 4), (4, 3), (4, 1), (3, 0), (1, 0)]],
    '&': [[(4, 0), (1, 4.4), (1, 5.4), (1.7, 6), (2.6, 6), (3.1, 5.4), (3.1, 4.7), (0, 2.4), (0, 1), (1, 0), (2.4, 0), (4, 2.4)]],
    ' ': [],
}
# the Deco cut: narrow, a high waist, pointed A and M (overrides; the rest are the plain strokes)
DECO = {
    'A': [[(0, 0), (2, 6), (4, 0)], [(.95, 2.85 + .6), (3.05, 2.85 + .6)]],
    'E': [[(4, 6), (0, 6), (0, 0), (4, 0)], [(0, 4.1), (2.6, 4.1)]],
    'H': [[(0, 0), (0, 6)], [(4, 0), (4, 6)], [(0, 4.1), (4, 4.1)]],
    'B': [[(0, 0), (0, 6), (3, 6), (4, 5.4), (4, 4.6), (3, 4.1), (0, 4.1)], [(3, 4.1), (4, 3.3), (4, .8), (3, 0), (0, 0)]],
    'R': [[(0, 0), (0, 6), (3, 6), (4, 5.4), (4, 4.6), (3, 4.0), (0, 4.0)], [(2, 4.0), (4, 0)]],
    'S': [[(4, 5.4), (3, 6), (1, 6), (0, 5.4), (0, 4.6), (1, 4.0), (3, 3.4), (4, 2.6), (4, .8), (3, 0), (1, 0), (0, .8)]],
    'M': [[(0, 0), (.6, 6), (2, 1.6), (3.4, 6), (4, 0)]],
    'K': [[(0, 0), (0, 6)], [(4, 6), (0, 3.2)], [(1.5, 4.3), (4, 0)]],
    'P': [[(0, 0), (0, 6), (3, 6), (4, 5.4), (4, 4.5), (3, 3.9), (0, 3.9)]],
}


def stroke_width(s, cell, deco=False):
    k = .62 if deco else 1.0
    return max(0.0, len(s) * 6 * cell * k - cell * k * 2)


def text_stroke(g, s, x, y, z, cell, f, T, style='cast', w=None, d=None, align='c', normal=(0, 0, -1), standoff=.06):
    """Stroke lettering on a face facing -z: a letter 4 x 6 cells, 2 cells apart; foot at y, centred at x (align 'c'). style
    'cast' (square bars `w` wide, `d` proud), 'deco' (narrower and thinner), 'neon' (glowing tubes on stand-offs, `f` a glow
    finish), 'flat' (flat strokes lying d off the face: LOD1's merged letters). Returns (x0, x1)."""
    deco = style == 'deco'
    kx = .62 if deco else 1.0
    width = stroke_width(s, cell, deco)
    x0 = x - width / 2 if align == 'c' else (x - width if align == 'r' else x)
    if w is None:
        w = cell * (.55 if deco else .9)
    if d is None:
        d = cell * .45
    for i, ch in enumerate(s):
        lx = x0 + i * 6 * cell * kx
        strokes = (DECO.get(ch) if deco else None) or STROKES.get(ch.upper() if ch != 'o' else ch, [])
        for st in strokes:
            pts = [(lx + p[0] * cell * kx, y + p[1] * cell) for p in st]
            for (ax, ay), (bx_, by) in zip(pts[:-1], pts[1:]):
                if style == 'neon':
                    zz = z - standoff
                    L = math.hypot(bx_ - ax, by - ay)
                    if L < 1e-6:
                        continue
                    ux, uy = (bx_ - ax) / L, (by - ay) / L
                    r = w / 2
                    rod(g, (ax - ux * r * .5, ay - uy * r * .5, zz), (bx_ + ux * r * .5, by + uy * r * .5, zz), r, f, (6, 5, 4)[min(2, T.i)], caps=T.detail)
                elif style == 'flat' or not T.detail:
                    _flat_bar(g, ax, ay, bx_, by, w, z - d, f)
                else:
                    _bar(g, ax, ay, bx_, by, w, z, z - d, f)
        if style == 'neon' and T.tiny:
            for st in strokes:
                for p in (st[0], st[-1]):
                    px_, py_ = lx + p[0] * cell * kx, y + p[1] * cell
                    rod(g, (px_, py_, z), (px_, py_, z - standoff + w * .4), w * .22, F['steel-dark'], 4, caps=False)
    return (x0, x0 + width)


def _bar(g, ax, ay, bx_, by, w, z0, z1, f):
    """A raised stroke from (ax, ay) to (bx, by) on a face at z0 standing out to z1 (z1 < z0), w wide, its ends square and
    lengthened by w/2 so joints close."""
    L = math.hypot(bx_ - ax, by - ay)
    if L < 1e-6:
        return
    ux, uy = (bx_ - ax) / L, (by - ay) / L
    nx, ny = -uy, ux
    h = w / 2
    a = (ax - ux * h, ay - uy * h)
    b = (bx_ + ux * h, by + uy * h)
    c = [(a[0] + nx * h, a[1] + ny * h), (b[0] + nx * h, b[1] + ny * h), (b[0] - nx * h, b[1] - ny * h), (a[0] - nx * h, a[1] - ny * h)]
    top = [(p[0], p[1], z1) for p in c]
    g.face(top, f, (0, 0, -1 if z1 < z0 else 1))
    for i in range(4):
        p, q = c[i], c[(i + 1) % 4]
        mx, my = (p[0] + q[0]) / 2 - (a[0] + b[0]) / 2, (p[1] + q[1]) / 2 - (a[1] + b[1]) / 2
        g.face(((p[0], p[1], z0), (q[0], q[1], z0), (q[0], q[1], z1), (p[0], p[1], z1)), f, _norm((mx, my, 0)))


def _flat_bar(g, ax, ay, bx_, by, w, z, f, n=-1):
    L = math.hypot(bx_ - ax, by - ay)
    if L < 1e-6:
        return
    ux, uy = (bx_ - ax) / L, (by - ay) / L
    nx, ny = -uy, ux
    h = w / 2
    a = (ax - ux * h, ay - uy * h)
    b = (bx_ + ux * h, by + uy * h)
    c = [(a[0] + nx * h, a[1] + ny * h), (b[0] + nx * h, b[1] + ny * h), (b[0] - nx * h, b[1] - ny * h), (a[0] - nx * h, a[1] - ny * h)]
    g.face([(p[0], p[1], z) for p in c], f, (0, 0, n))


def placed(g, build, q=(0.0, 0.0, 0.0, 1.0), t=(0.0, 0.0, 0.0)):
    """Builds `build(sub)` in the canonical frame (facing -z at the origin) and places it."""
    sub = Geo()
    build(sub)
    part(g, sub, q, t)


# ---------------------------------------------------------------------------------------------------------------------------
# Clocks: every clock reads 6:47 (bible 1.3): the minute hand at 282 degrees from twelve, the hour hand at 203.5
# ---------------------------------------------------------------------------------------------------------------------------
MINUTE_ANGLE, HOUR_ANGLE = 282.0, 203.5


def clock(g, T, R, face_f, rim_f, hand_f, mark_f, roman=False, depth=.08, rim=.08, ticks=True, boss_f=None):
    """A clock face of radius R at the origin facing -z (its dial at z = -depth): a rim, the dial (lit or not, its finish
    face_f), 12 hour marks (bars or Roman numerals), 48 minute dots (LOD0), the hands at 6:47 and a boss."""
    n = max(12, T.arc * 2 if T.lod == 0 else 12)
    # rim: a ring standing proud of the dial, and the back
    prof = [(R + rim, 0.0), (R + rim, depth + .03), (R + rim * .35, depth + .05), (R, depth + .02)] if T.detail else \
        [(R + rim, 0.0), (R + rim, depth + .04)]
    _ring_lathe(g, prof, rim_f, n)
    g.face([(math.cos(2 * PI * i / n) * (R + rim), math.sin(2 * PI * i / n) * (R + rim), 0.0) for i in range(n)][::-1], rim_f, (0, 0, 1))
    dial = [(math.cos(2 * PI * i / n) * R, math.sin(2 * PI * i / n) * R, -depth) for i in range(n)]
    g.face(dial, face_f, (0, 0, -1))
    z = -depth - .004
    for k in range(12):
        if not T.detail and k % 3:
            continue
        a = 2 * PI * k / 12
        sx, cy = math.sin(a), math.cos(a)
        if roman and T.detail:
            _roman(g, k if k else 12, sx * R * .78, cy * R * .78, z, R * .15, mark_f, a)
        else:
            l0, l1 = R * (.78 if k % 3 else .72), R * .9
            wv = R * (.05 if k % 3 else .075)
            _flat_bar(g, sx * l0, cy * l0, sx * l1, cy * l1, wv, z, mark_f)
    if ticks and T.tiny:
        for k in range(60):
            if k % 5 == 0:
                continue
            a = 2 * PI * k / 60
            sx, cy = math.sin(a), math.cos(a)
            _flat_bar(g, sx * R * .86, cy * R * .86, sx * R * .9, cy * R * .9, R * .018, z, mark_f)
    # the hands (flat, layered over the dial): hour broad and short, minute long and slim
    zh = -depth - .012
    _hand(g, HOUR_ANGLE, R * .52, R * .075, R * .14, zh, hand_f)
    _hand(g, MINUTE_ANGLE, R * .8, R * .045, R * .16, zh - .006, hand_f)
    bf = hand_f if boss_f is None else boss_f
    m = max(6, n // 3)
    g.face([(math.cos(2 * PI * i / m) * R * .06, math.sin(2 * PI * i / m) * R * .06, zh - .012) for i in range(m)], bf, (0, 0, -1))


def _ring_lathe(g, prof, f, n):
    """A ring about the z axis from (radius, z-depth-out) points: profile points (r, out) with out toward -z."""
    rings = []
    for (r, o) in prof:
        rings.append([(math.cos(2 * PI * i / n) * r, math.sin(2 * PI * i / n) * r, -o) for i in range(n)])
    for j in range(len(prof) - 1):
        (r0, o0), (r1, o1) = prof[j], prof[j + 1]
        dr, do = r1 - r0, o1 - o0
        for i in range(n):
            i1 = (i + 1) % n
            a = 2 * PI * (i + .5) / n
            # outward in (radial, -z): perpendicular to (dr, do)
            nr, no = do, -dr
            l = math.hypot(nr, no) or 1
            nrm = (math.cos(a) * nr / l, math.sin(a) * nr / l, -no / l)
            g.face((rings[j][i], rings[j][i1], rings[j + 1][i1], rings[j + 1][i]), f, nrm)


def _hand(g, deg, length, width, tail, z, f):
    a = math.radians(deg)
    dx, dy = math.sin(a), math.cos(a)
    nx, ny = -dy, dx
    tip = (dx * length, dy * length)
    back = (-dx * tail, -dy * tail)
    w = width / 2
    pts = [(back[0] + nx * w * .7, back[1] + ny * w * .7), (dx * length * .62 + nx * w, dy * length * .62 + ny * w), tip,
           (dx * length * .62 - nx * w, dy * length * .62 - ny * w), (back[0] - nx * w * .7, back[1] - ny * w * .7)]
    g.face([(p[0], p[1], z) for p in pts], f, (0, 0, -1))


ROMAN = {1: 'I', 2: 'II', 3: 'III', 4: 'IV', 5: 'V', 6: 'VI', 7: 'VII', 8: 'VIII', 9: 'IX', 10: 'X', 11: 'XI', 12: 'XII'}


def _roman(g, k, cx, cy, z, h, f, ang):
    """A Roman numeral as flat strokes, set radially upright (each numeral's foot toward the centre)."""
    s = ROMAN[k]
    w = h * .14
    unit = h * .42
    total = 0.0
    for ch in s:
        total += {'I': unit * .35, 'V': unit, 'X': unit}[ch] + unit * .25
    total -= unit * .25
    x = -total / 2
    up = (math.sin(ang), math.cos(ang))
    rt = (up[1], -up[0])

    def P(a, b):
        return (cx + rt[0] * a + up[0] * b, cy + rt[1] * a + up[1] * b)
    for ch in s:
        if ch == 'I':
            a, b = P(x + unit * .175, -h / 2), P(x + unit * .175, h / 2)
            _flat_bar(g, a[0], a[1], b[0], b[1], w, z, f)
            x += unit * .35 + unit * .25
        elif ch == 'V':
            for (p, q) in (((x, h / 2), (x + unit / 2, -h / 2)), ((x + unit / 2, -h / 2), (x + unit, h / 2))):
                a, b = P(*p), P(*q)
                _flat_bar(g, a[0], a[1], b[0], b[1], w, z, f)
            x += unit + unit * .25
        else:
            for (p, q) in (((x, h / 2), (x + unit, -h / 2)), ((x, -h / 2), (x + unit, h / 2))):
                a, b = P(*p), P(*q)
                _flat_bar(g, a[0], a[1], b[0], b[1], w, z, f)
            x += unit + unit * .25


# ---------------------------------------------------------------------------------------------------------------------------
# Small shared pieces: rivets, lamps, plates
# ---------------------------------------------------------------------------------------------------------------------------
def rivet(g, T, p, n, r, f):
    """A round-head rivet at p on a face with normal n: a low six-sided cone (LOD0's tiniest parts only)."""
    if not T.tiny:
        return
    u, v, w = sk.frame((0, 0, 0), n)
    apex = _add(p, w, r * .55)
    ring = [_add(_add(p, u, math.cos(2 * PI * i / 6) * r), v, math.sin(2 * PI * i / 6) * r) for i in range(6)]
    for i in range(6):
        a, b = ring[i], ring[(i + 1) % 6]
        mid = _norm(_add(_sub(_lerp(a, b, .5), p), w, r * 1.2))
        g.face((a, b, apex), f, mid)


def rivet_row(g, T, a, b, n, r, f, step=.12):
    if not T.tiny:
        return
    L = math.sqrt(_dot(_sub(b, a), _sub(b, a)))
    k = max(1, int(L / step))
    for i in range(k + 1):
        rivet(g, T, _lerp(a, b, i / k), n, r, f)


def plate_text(g, T, s, x, y, z, w, h, f_plate, f_text, px=None, style='flat', border=None, depth=.015):
    """An enamel or steel plate (w x h centred at x, foot at y) on a face at z facing -z, with 5 x 7 lettering."""
    bx(g, T, x - w / 2, x + w / 2, y, y + h, z - depth, z, f_plate, bevel=.006, cell=0, skip=('+z',))
    if border is not None and T.detail:
        e = min(w, h) * .06
        for (a0, a1, b0, b1) in ((x - w / 2 + e, x + w / 2 - e, y + e, y + e * 1.6), (x - w / 2 + e, x + w / 2 - e, y + h - e * 1.6, y + h - e),
                                 (x - w / 2 + e, x - w / 2 + e * 1.6, y + e, y + h - e), (x + w / 2 - e * 1.6, x + w / 2 - e, y + e, y + h - e)):
            g.face(((a0, b0, z - depth - .002), (a1, b0, z - depth - .002), (a1, b1, z - depth - .002), (a0, b1, z - depth - .002)), border, (0, 0, -1))
    if T.detail:
        if px is None:
            px = h * .5 / 7
        text_pix(g, s, x, y + (h - 7 * px) / 2, z - depth, px, f_text, style=style, depth=.003)


def caged_lamp(g, T, f_body, f_glass, f_cage, length=.32, r=.11):
    """A bulkhead lamp in a wire cage at the origin, its axis along -z (out of a wall): a cast base, the glass dome (glowing),
    the cage's hoops and bars."""
    sides = T.small + 2
    sk.lathe(g, [(r * 1.25, 0.0), (r * 1.25, .05), (r * 1.05, .07)], (0, 0, 0), f_body, sides, axis=(0, 0, -1), cap1=True)
    sk.lathe(g, [(r * .9, .07), (r * .95, .07 + length * .45), (r * .7, .07 + length * .8), (0.0, .07 + length * .92)], (0, 0, 0), f_glass, sides, axis=(0, 0, -1))
    if T.detail:
        for k in range(4):
            a = 2 * PI * k / 4 + PI / 4
            pts = [(math.cos(a) * r * 1.12, math.sin(a) * r * 1.12, -.07), (math.cos(a) * r * 1.12, math.sin(a) * r * 1.12, -.07 - length * .55),
                   (0, 0, -.07 - length * 1.02)]
            for p, q in zip(pts[:-1], pts[1:]):
                rod(g, p, q, max(.004, r * .07), f_cage, 4, caps=False)
        n = 6
        zz, rr = -.07 - length * .55, r * 1.12
        for i in range(n):
            a0, a1 = 2 * PI * i / n + PI / 4, 2 * PI * (i + 1) / n + PI / 4
            rod(g, (math.cos(a0) * rr, math.sin(a0) * rr, zz), (math.cos(a1) * rr, math.sin(a1) * rr, zz), max(.004, r * .065), f_cage, 4, caps=False)


def beacon(g, T, f_base, f_dome, f_cage=None, r=.13, h=.24):
    """A turning beacon on top of a surface at the origin (axis +y): a base and a lens dome (glowing)."""
    sides = T.small + 4
    sk.lathe(g, [(r * 1.2, 0.0), (r * 1.2, .06), (r * 1.0, .08)], (0, 0, 0), f_base, sides, cap1=True)
    sk.lathe(g, [(r * .95, .08), (r * .95, .08 + h * .55), (r * .7, .08 + h * .85), (0.0, .08 + h)], (0, 0, 0), f_dome, sides)
    if f_cage is not None and T.detail:
        for k in range(4):
            a = 2 * PI * k / 4
            rod(g, (math.cos(a) * r * 1.05, .08, math.sin(a) * r * 1.05), (math.cos(a) * r * 1.05, .08 + h * .7, math.sin(a) * r * 1.05), .008, f_cage, 4, caps=False)


def flood(g, T, f_body, f_lens, w=.5, h=.36, d=.2):
    """A floodlight head at the origin, its lens toward -z: a housing with fins and the glowing lens."""
    bx(g, T, -w / 2, w / 2, -h / 2, h / 2, -d / 2, d / 2, f_body, bevel=.02, cell=0)
    g.face(((-w / 2 + .03, -h / 2 + .03, -d / 2 - .004), (w / 2 - .03, -h / 2 + .03, -d / 2 - .004), (w / 2 - .03, h / 2 - .03, -d / 2 - .004),
            (-w / 2 + .03, h / 2 - .03, -d / 2 - .004)), f_lens, (0, 0, -1))
    if T.detail:
        for i in range(5):
            x = -w / 2 + .06 + i * (w - .12) / 4
            bx(g, T, x - .008, x + .008, -h / 2, h / 2, d / 2, d / 2 + .06, f_body, bevel=0, cell=0)
        # the visor
        bx(g, T, -w / 2 - .01, w / 2 + .01, h / 2, h / 2 + .012, -d / 2 - .12, -d / 2 + .02, f_body, bevel=0, cell=0)


def join_ring(g, T, cz, f_ring, f_mark, r0=3.45, r1=3.75, marks=8, y=.004, cx=0.0):
    """The join circle's surround: a flat ring outside its 3.3 m keep-out (r0 >= 3.4), and its marks round it."""
    n = max(24, T.arc * 4)
    for i in range(n):
        a0, a1 = 2 * PI * i / n, 2 * PI * (i + 1) / n
        p = [(cx + math.cos(a0) * r0, cz + math.sin(a0) * r0), (cx + math.cos(a1) * r0, cz + math.sin(a1) * r0),
             (cx + math.cos(a1) * r1, cz + math.sin(a1) * r1), (cx + math.cos(a0) * r1, cz + math.sin(a0) * r1)]
        flat_poly(g, p, y, f_ring)
    if f_mark is not None:
        for k in range(marks):
            a = 2 * PI * k / marks + PI / marks
            ca, sa = math.cos(a), math.sin(a)
            ta, tb = -sa, ca
            rr0, rr1 = r1 + .05, r1 + .45
            w = .09
            p = [(cx + ca * rr0 + ta * w, cz + sa * rr0 + tb * w), (cx + ca * rr1 + ta * w, cz + sa * rr1 + tb * w),
                 (cx + ca * rr1 - ta * w, cz + sa * rr1 - tb * w), (cx + ca * rr0 - ta * w, cz + sa * rr0 - tb * w)]
            flat_poly(g, p[::-1], y + .002, f_mark)


def disc(g, cx, cy, cz, r, n, f, normal=(0, 0, -1)):
    """A flat disc facing `normal` (an axis direction)."""
    u, v, w = sk.frame((0, 0, 0), normal)
    g.face([(cx + (u[0] * math.cos(2 * PI * i / n) + v[0] * math.sin(2 * PI * i / n)) * r,
             cy + (u[1] * math.cos(2 * PI * i / n) + v[1] * math.sin(2 * PI * i / n)) * r,
             cz + (u[2] * math.cos(2 * PI * i / n) + v[2] * math.sin(2 * PI * i / n)) * r) for i in range(n)], f, normal)


# ---------------------------------------------------------------------------------------------------------------------------
# A module: its static geometry, its rig parts, its practical lights and its markers
# ---------------------------------------------------------------------------------------------------------------------------
class Module:
    """What a builder returns: static Geo (the bank's block or static art), rig parts {name: (Geo, pivot, axis/kind)} in the
    module's frame at rest (posed by Core), practical lights [(pos, hex, watts, radius, kind, dir)] for the light field and the
    renders, markers {name: pos} (the loot glow, the join circle), soot plumes [(x, y, z, k, rise, width)] for the bake."""

    def __init__(self, name, lod):
        self.name, self.lod = name, lod
        self.static = Geo()
        self.rigs = {}
        self.lights = []
        self.markers = {}
        self.soot = []
        self.notes = {}
        self.subs = {}

    def sub(self, name):
        """A named static part, kept apart from `static` so its triangles are counted on their own (it banks with the
        static art: block/... or a far mesh)."""
        if name not in self.subs:
            self.subs[name] = Geo()
        return self.subs[name]

    def rig(self, name, pivot=(0.0, 0.0, 0.0), motion='slide', axis=(1.0, 0.0, 0.0), travel=0.0):
        g = Geo()
        self.rigs[name] = {'geo': g, 'pivot': pivot, 'motion': motion, 'axis': axis, 'travel': travel}
        return g

    def light(self, pos, hexc, gain, radius, kind='point', direction=None, cone=None, circuit=0, part=None, note=''):
        """A light in the game's terms (bible 3, 6.2): a light-field pool or a practical slot. kind 'point' or 'spot' (no area
        lights: the game has none); gain multiplies the colour in the light field's sum (sodium 3.4 at 9.5 m is the street's
        yardstick); radius is where its (1 - (d/r)^2)^2 falloff ends; a spot's cone is (outer, inner) full angles in degrees
        about `direction`; circuit 0 steady, 1 flicker, 2 district, 3 alarm; part: the rig part whose glow it follows."""
        assert kind in ('point', 'spot'), kind
        self.lights.append({'pos': tuple(pos), 'hex': hexc, 'gain': gain, 'radius': radius, 'kind': kind,
                            'dir': direction, 'cone': cone, 'circuit': circuit, 'part': part, 'note': note})

    def tris(self):
        def count(g):
            return int(sum(s - 2 for s in g.size))
        out = {'static': count(self.static)}
        for k, g in self.subs.items():
            out[k] = count(g)
        for k, r in self.rigs.items():
            out[k] = count(r['geo'])
        out['total'] = sum(out.values())
        return out


def arch_ring(g, T, cx, cy, z, r0, r1, proud, f, legs=True, y0=0.0, n=-1, inner=None, outer=None):
    """A round arch's ring on a wall at z facing -z: the band between radii r0 and r1 over the springing (cx, cy), standing
    `proud` out of the wall, with its legs (the same band, upright) down to y0. Its face, its intrados and extrados."""
    k = max(4, T.arc)
    zf = z - proud
    if inner is None:
        inner = T.detail or proud > .15
    if outer is None:
        outer = T.detail or proud > .15
    pts = [(math.pi * (1 - i / k)) for i in range(k + 1)]
    for i in range(k):
        a0, a1 = pts[i], pts[i + 1]
        c0, s0, c1, s1 = math.cos(a0), math.sin(a0), math.cos(a1), math.sin(a1)
        P = lambda r, c, s, zz: (cx + r * c, cy + r * s, zz)
        g.face((P(r0, c0, s0, zf), P(r1, c0, s0, zf), P(r1, c1, s1, zf), P(r0, c1, s1, zf)), f, (0, 0, -1))
        am = (a0 + a1) / 2
        if inner:
            g.face((P(r0, c0, s0, z), P(r0, c1, s1, z), P(r0, c1, s1, zf), P(r0, c0, s0, zf)), f, (-math.cos(am), -math.sin(am), 0))
        if outer:
            g.face((P(r1, c0, s0, z), P(r1, c1, s1, z), P(r1, c1, s1, zf), P(r1, c0, s0, zf)), f, (math.cos(am), math.sin(am), 0))
    if legs and cy > y0 + 1e-4:
        for sx in (-1, 1):
            xa, xb = cx + sx * r0, cx + sx * r1
            a, b = min(xa, xb), max(xa, xb)
            c = cell_at(T, y0)
            sk.grid_face(g, (a, y0, zf), (b - a, 0, 0), (0, cy - y0, 0), 0, 0, f, (0, 0, -1), c)
            if inner:
                g.face(((xa, y0, z), (xa, cy, z), (xa, cy, zf), (xa, y0, zf)), f, (-sx, 0, 0))
            if outer:
                g.face(((xb, y0, z), (xb, cy, z), (xb, cy, zf), (xb, y0, zf)), f, (sx, 0, 0))


def glass_blocks(g, T, x0, x1, y0, y1, z, f_glass, f_grid, size=.2, n=-1):
    """A panel of glass blocks lit from behind: the glowing field and, on LOD0, its mortar grid laid over it."""
    g.face(((x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z)), f_glass, (0, 0, n))
    if not T.detail:
        return
    w = size * .09
    zz = z + n * .003
    nx = max(1, int(round((x1 - x0) / size)))
    ny = max(1, int(round((y1 - y0) / size)))
    for i in range(1, nx):
        x = x0 + (x1 - x0) * i / nx
        g.face(((x - w, y0, zz), (x + w, y0, zz), (x + w, y1, zz), (x - w, y1, zz)), f_grid, (0, 0, n))
    for j in range(1, ny):
        y = y0 + (y1 - y0) * j / ny
        g.face(((x0, y - w, zz), (x1, y - w, zz), (x1, y + w, zz), (x0, y + w, zz)), f_grid, (0, 0, n))
