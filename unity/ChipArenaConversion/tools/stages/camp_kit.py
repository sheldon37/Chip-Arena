"""Belvedere Camp's kit (tools/stages/camp_bank.py builds the bank ChipArenaCamp from it; docs/expedition/BELVEDERE-CAMP.md).

Run inside Blender 5.x (numpy); geometry is stagekit's corner geometry, with the Meridian gates kit's finishes, sweeps and
lettering (meridian_gates_kit.py: tubes there turn their faces outward). The camp is the survivors' improvisation (art bible 1.6,
3.4): mismatched corrugated sheet, plywood, timber, tarps, sandbags, bulb strings and spray paint, warm light against the cold city.

Frames (the runtime places every prop with Core NativeCamp's 3 x 4 rows):
  fence props   origin on the fence's inner face at floor level; +x the viewer's right facing the fence, +y up, +z INTO the fence;
                the art faces -z (the camp). Nothing below 3.4 m reaches further than its piece's base box (Core NativeCamp).
  structures    origin at the A1 row's centre on the floor, axis-aligned; the art hugs its block (within 0.25 m).
  figures       feet on y 0, facing +z, +x their right.
"""
import math

import stagekit as sk
from stagekit import Geo
import meridian_gates_kit as mk

PI = math.pi
F = {}


def finishes():
    """The camp's finishes by name, on top of the Meridian kit's (mk.F)."""
    if F:
        return F
    mk.finishes()
    F.update(mk.F)
    W, CL, I, N, P, FL = sk.DETAIL_WOOD, sk.DETAIL_CLOTH, sk.DETAIL_IRON, sk.DETAIL_NONE, sk.DETAIL_PLASTER, sk.DETAIL_FLAG

    def a(name, hexc, detail=N, smooth=.2, metal=0.0, dirt=1.0, moss=0.0, wear=0.0, param=0.0):
        F[name] = sk.fin('camp-' + name, hexc, detail, smooth=smooth, metal=metal, dirt=dirt, moss=moss, param=param, wear=wear)
    # the survivors' livery (art bible 3.4)
    a('sheet-rust', 0x8A4A2A, N, .30, .4, 1.0, 0, .30)
    a('sheet-teal', 0x3A7A7A, N, .32, .4, .9, 0, .35)
    a('sheet-cream', 0xCFC2A0, N, .30, .3, 1.0, 0, .35)
    a('sheet-red', 0x9A3A2E, N, .30, .3, 1.0, 0, .35)
    a('sheet-galv', 0x8E9498, N, .45, .7, .9, 0, .15)
    a('tarp-blue', 0x4A5A6E, CL, .15, 0, .7, 0, 0)
    a('tarp-olive', 0x6A6A3E, CL, .12, 0, .8, 0, 0)
    a('tarp-rust', 0x8A4A2A, CL, .12, 0, .8, 0, 0)
    a('tarp-teal', 0x3A7A7A, CL, .12, 0, .8, 0, 0)
    a('canvas-stripe', 0xC9B48A, CL, .10, 0, .8, 0, 0)
    a('canvas-red', 0x9A3A2E, CL, .10, 0, .8, 0, 0)
    a('ply', 0xB79B72, W, .15, 0, 1.0, .05, .10, 0)
    a('ply-dark', 0x8C7452, W, .15, 0, 1.0, .05, .10, 0)
    a('timber', 0x7A5E42, W, .15, 0, 1.0, .20, .12, 1)
    a('timber-dark', 0x5A4430, W, .15, 0, 1.0, .20, .12, 1)
    a('scaffold', 0x8C9296, N, .45, .75, .7, 0, .25)
    a('clamp', 0x6E7378, N, .40, .8, .8, 0, .30)
    a('drum', 0x3E3A36, I, .30, .6, 1.2, 0, .25)
    a('drum-char', 0x1E1A18, N, .20, .3, .8, 0, 0)
    a('soot', 0x161412, N, .15, 0, 0, 0, 0)
    a('ash', 0x6A6560, N, .10, 0, .5, 0, 0)
    a('log', 0x5A4632, W, .10, 0, .9, 0, .15, 1)
    a('cd-olive', 0x5B6B3A, N, .40, .3, .8, 0, .35)
    a('cd-orange', 0xEE7420, N, .45, .2, .7, 0, .35)
    a('cd-white', 0xECEBE2, N, .35, 0, .6, 0, .30)
    a('spray-white', 0xF0EEE6, N, .25, 0, .4, 0, .2)
    a('spray-orange', 0xFF8A3A, N, .25, 0, .4, 0, .2)
    a('paper', 0xE8E2CE, N, .10, 0, .5, 0, 0)
    a('paper-old', 0xD6CCA8, N, .10, 0, .7, 0, 0)
    a('paper-pink', 0xE8B8B0, N, .10, 0, .5, 0, 0)
    a('paper-blue', 0xB8C8D8, N, .10, 0, .5, 0, 0)
    a('ink', 0x2A2420, N, .20, 0, .2, 0, 0)
    a('ink-red', 0xB02A24, N, .25, 0, .3, 0, 0)
    a('chalk', 0xE8E6D8, N, .10, 0, .3, 0, 0)
    a('chalk-yellow', 0xE8C870, N, .10, 0, .3, 0, 0)
    a('pin-red', 0xC0282A, N, .60, .1, .2, 0, 0)
    a('pin-blue', 0x2A5AC0, N, .60, .1, .2, 0, 0)
    a('photo', 0x7A7468, N, .40, 0, .3, 0, 0)
    a('photo-dark', 0x3A3632, N, .40, 0, .3, 0, 0)
    a('cable', 0x18181A, N, .45, 0, .4, 0, 0)
    a('rope', 0x8E8670, CL, .15, 0, .8, 0, .1)
    a('sandbag', 0x8A7A58, CL, .10, 0, 1.2, .10, .05)
    a('sandbag-dark', 0x6E6046, CL, .10, 0, 1.2, .10, .05)
    a('stone', 0xA8A090, FL, .25, 0, 1.0, .25, .25, .5)
    a('stone-dark', 0x7A7468, FL, .25, 0, 1.0, .20, .20, .5)
    a('water-dry', 0x3A3630, N, .30, 0, 1.0, .20, 0)
    a('panel-white', 0xD8D6CC, N, .35, .1, .8, 0, .2)
    a('panel-grey', 0x8A8C88, N, .35, .3, .8, 0, .2)
    a('red-cross', 0xB0232A, N, .35, 0, .4, 0, .2)
    a('mesh', 0x5A5E60, N, .40, .7, .8, 0, .2)
    a('glass', 0x2A3A48, N, .90, 0, .1, 0, 0)
    a('generator', 0x3E5A44, N, .45, .4, .9, 0, .35)
    a('crate', 0x7A6448, W, .15, 0, 1.0, .05, .15, 0)
    a('blanket-grey', 0x7A7468, CL, .08, 0, .6, 0, 0)
    a('blanket-red', 0x8A3A30, CL, .08, 0, .6, 0, 0)
    a('blanket-green', 0x4A5A3A, CL, .08, 0, .6, 0, 0)
    # people (the camp's own look: lanterns, armbands, blankets; no district's zombie clothes)
    for name, hexc in (('skin-a', 0xE0B89A), ('skin-b', 0xB07A56), ('skin-c', 0x7A4E36), ('hair-a', 0x2A1E16), ('hair-b', 0x5A3A20),
                       ('hair-c', 0x9A9690), ('eye', 0x141214)):
        a(name, hexc, N, .35 if name.startswith('skin') else .25, 0, .1, 0, 0)
    for name, hexc in (('coat-rust', 0x8A4A2A), ('coat-teal', 0x3A6E6E), ('coat-olive', 0x5E6236), ('coat-navy', 0x2E3A52),
                       ('coat-red', 0x8A3028), ('coat-grey', 0x5E5C58), ('coat-white', 0xE6E4DA), ('coat-brown', 0x6A4E36),
                       ('coat-maroon', 0x6A2626), ('scrubs', 0x5A7AA8), ('polo-teal', 0x2E8A8A), ('shirt', 0xD8D2C0),
                       ('trousers-a', 0x2A2A30), ('trousers-b', 0x2E3A52), ('trousers-c', 0x4A3A2A), ('boots', 0x24201C),
                       ('apron', 0x5A4632), ('overall', 0x8A5A2A), ('beanie-a', 0x8A3028), ('beanie-b', 0x3A5A6A),
                       ('helmet', 0x5B6B3A), ('armband', 0xFF8A3A), ('scarf', 0xB8A060)):
        a(name, hexc, CL, .12, 0, .5, 0, .05)
    a('rifle', 0x2A2826, N, .45, .6, .4, 0, .3)
    a('rifle-wood', 0x6A4A2E, W, .35, 0, .4, 0, .2, 0)
    a('metal-dark', 0x34383C, N, .45, .6, .6, 0, .25)
    return F


def glow(hexc, gain):
    return mk.glow(hexc, gain)


BULB = 0xFFC274
LANTERN = 0xFFB054
FLOOD = 0xFFD9A8
EMBER = 0xFF6A1E


# ---------------------------------------------------------------------------------------------------------------------------
# Shared parts
# ---------------------------------------------------------------------------------------------------------------------------
def bx(g, T, x0, x1, y0, y1, z0, z1, f, bevel=.012, cell=None, skip=()):
    mk.bx(g, T, min(x0, x1), max(x0, x1), min(y0, y1), max(y0, y1), min(z0, z1), max(z0, z1), f, bevel=bevel, cell=cell, skip=skip)


def rod(g, a, b, r, f, sides=6, caps=True, r1=None):
    mk.rod(g, a, b, r, f, sides, caps=caps, r1=r1)


def tube(g, pts, r, f, sides=6, caps=True):
    mk.tube_path(g, pts, r, f, sides, caps=caps)


def sphere(g, c, r, f, sides=8, rings=4, squash=1.0):
    mk.ellipsoid(g, c, (r, r * squash), f, sides, rings=rings)


def corrugated(g, T, x0, x1, y0, y1, z, f, n=-1, pitch=.076, depth=.016, vertical=True):
    """A corrugated sheet in the plane z facing -z (n -1) or +z: its ribs (vertical, or horizontal) modelled on LOD0, flat on LOD1.
    The sheet lies between z and z + n*depth (it never stands proud of its plane by more than `depth`)."""
    if not T.detail:
        mk.wall_rect(g, T, x0, x1, y0, y1, z + n * depth * .5, f, n=n, cell=0)
        return
    zr, zv = z + n * depth, z
    if vertical:
        k = max(2, int(round((x1 - x0) / pitch)))
        for i in range(k):
            a0, a1, a2 = (x0 + (x1 - x0) * (i + t) / k for t in (0, .5, 1))
            g.face(((a0, y0, zv), (a1, y0, zr), (a1, y1, zr), (a0, y1, zv)), f, normal=(0, 0, n))
            g.face(((a1, y0, zr), (a2, y0, zv), (a2, y1, zv), (a1, y1, zr)), f, normal=(0, 0, n))
    else:
        k = max(2, int(round((y1 - y0) / pitch)))
        for i in range(k):
            b0, b1, b2 = (y0 + (y1 - y0) * (i + t) / k for t in (0, .5, 1))
            g.face(((x0, b0, zv), (x1, b0, zv), (x1, b1, zr), (x0, b1, zr)), f, normal=(0, 0, n))
            g.face(((x0, b1, zr), (x1, b1, zr), (x1, b2, zv), (x0, b2, zv)), f, normal=(0, 0, n))


def side_corrugated(g, T, z0, z1, y0, y1, x, f, n=1, pitch=.076, depth=.016):
    """A corrugated sheet in the plane x facing +x (n 1) or -x: vertical ribs, never more than `depth` off its plane."""
    if not T.detail:
        mk.side_rect(g, T, min(z0, z1), max(z0, z1), y0, y1, x + n * depth * .5, f, n=n, cell=0)
        return
    xr, xv = x + n * depth, x
    k = max(2, int(round(abs(z1 - z0) / pitch)))
    for i in range(k):
        a0, a1, a2 = (z0 + (z1 - z0) * (i + t) / k for t in (0, .5, 1))
        g.face(((xv, y0, a0), (xr, y0, a1), (xr, y1, a1), (xv, y1, a0)), f, normal=(n, 0, 0))
        g.face(((xr, y0, a1), (xv, y0, a2), (xv, y1, a2), (xr, y1, a1)), f, normal=(n, 0, 0))


def lettering(g, T, s, x, y, z, px, f, depth=.004, align='c', style='flat'):
    """5 x 7 letters on a face facing -z (spray paint and stencils lie 4 mm off their face)."""
    return mk.text_pix(g, s, x, y, z, px, f, style=style, depth=depth, align=align, T=T)


def hand_lantern(g, T, x, y, z, s=1.0, lit=True):
    """A hurricane lantern standing on (x, y, z): its tin base and cap, the globe (glowing when lit), the bail handle."""
    F_ = finishes()
    tin, glassf = F_['metal-dark'], (glow(LANTERN, 2.6) if lit else F_['glass'])
    sides = 8 if T.detail else 6
    sk.lathe(g, [(.07 * s, 0), (.075 * s, .03 * s), (.06 * s, .045 * s)], (x, y, z), tin, sides, cap0=True, cap1=False)
    sk.lathe(g, [(.045 * s, .045 * s), (.06 * s, .1 * s), (.055 * s, .15 * s), (.04 * s, .18 * s)], (x, y, z), glassf, sides)
    sk.lathe(g, [(.06 * s, .18 * s), (.065 * s, .2 * s), (.03 * s, .24 * s), (.015 * s, .26 * s)], (x, y, z), tin, sides, cap1=True)
    if T.detail:
        pts = [(x - .07 * s, y + .19 * s, z), (x - .05 * s, y + .3 * s, z), (x, y + .33 * s, z), (x + .05 * s, y + .3 * s, z), (x + .07 * s, y + .19 * s, z)]
        tube(g, pts, .005 * s, tin, 4, caps=False)


def bulb(g, T, p, lit=True, s=1.0):
    """A festoon bulb hanging at p: its socket up to the wire and the glass (glowing, or dark when dead)."""
    F_ = finishes()
    x, y, z = p
    rod(g, (x, y + .05 * s, z), (x, y + .11 * s, z), .012 * s, F_['cable'], 5)
    sides = 5 if T.detail else 4
    sk.lathe(g, [(.0, -.045 * s), (.03 * s, -.02 * s), (.032 * s, .015 * s), (.012 * s, .05 * s)], (x, y, z),
             glow(BULB, 3.0) if lit else F_['glass'], sides)


def clamp(g, T, x, y, z, w=.1, h=.06, d=.08, f=None):
    """A scaffold clamp or bracket block centred at (x, y, z)."""
    bx(g, T, x - w / 2, x + w / 2, y - h / 2, y + h / 2, z - d / 2, z + d / 2, f or finishes()['clamp'], bevel=.006, cell=0)


def caged_lamp(g, T, at, down=(0, -1, 0), lit=True, r=.085, length=.24):
    """A caged bulkhead lamp at `at`, its glass toward `down` (a unit vector)."""
    F_ = finishes()
    sub = Geo()
    mk.caged_lamp(sub, T, F_['metal-dark'], glow(LANTERN, 2.8) if lit else F_['glass'], F_['clamp'], length=length, r=r)
    # the kit's lamp points along -z: turn -z onto `down`
    d = sk._norm(down)
    axis = sk._cross((0, 0, -1), d)
    s_ = math.sqrt(sk._dot(axis, axis))
    if s_ < 1e-6:
        q = (0, 0, 0, 1) if d[2] < 0 else mk.qy(180)
    else:
        ang = math.atan2(s_, sk._dot((0, 0, -1), d))
        q = sk.quat_axis(axis[0] / s_, axis[1] / s_, axis[2] / s_, ang)
    mk.part(g, sub, q, at)


def flood_head(g, T, at, aim, lit=True, w=.42, h=.3, d=.18):
    """A floodlight head at `at` aimed along `aim`: housing, glowing lens, fins and visor; on a short yoke."""
    F_ = finishes()
    sub = Geo()
    mk.flood(sub, T, F_['cd-olive'], glow(FLOOD, 3.2) if lit else F_['glass'], w=w, h=h, d=d)
    d_ = sk._norm(aim)
    yaw = math.degrees(math.atan2(-d_[0], -d_[2]))
    pitch = math.degrees(math.asin(max(-1, min(1, -d_[1]))))
    q = mk.qmul(mk.qy(yaw), mk.qx(-pitch))
    mk.part(g, sub, q, at)


# ---------------------------------------------------------------------------------------------------------------------------
# The survivors: rigid posed figures (the preview's, and W5's figures beyond 30 m)
# ---------------------------------------------------------------------------------------------------------------------------
def _v(a, b):
    return (b[0] - a[0], b[1] - a[1], b[2] - a[2])


def _add(a, b, k=1.0):
    return (a[0] + b[0] * k, a[1] + b[1] * k, a[2] + b[2] * k)


def _mid(a, b, t=.5):
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t)


def limb(g, a, b, r0, r1, f, sides):
    rod(g, a, b, r0, f, sides, caps=False, r1=r1)


# A pose: the joints in the figure's frame (feet on y 0, facing +z). Every pose is built from a stance (standing or seated) with
# its arms, head turn and props.
STAND = dict(pelvis=(0, .95, 0), neck=(0, 1.44, -.01), head=(0, 1.6, .02), hipL=(-.1, .92, 0), hipR=(.1, .92, 0),
             kneeL=(-.11, .5, .03), kneeR=(.11, .5, .03), ankleL=(-.115, .085, 0), ankleR=(.115, .085, 0),
             elbowL=(-.24, 1.13, -.01), elbowR=(.24, 1.13, -.01), handL=(-.26, .87, .03), handR=(.26, .87, .03))
SEAT = dict(pelvis=(0, .5, -.06), neck=(0, .99, -.09), head=(0, 1.15, -.05), hipL=(-.11, .5, -.04), hipR=(.11, .5, -.04),
            kneeL=(-.13, .52, .32), kneeR=(.13, .52, .32), ankleL=(-.13, .085, .36), ankleR=(.13, .085, .36),
            elbowL=(-.24, .72, .02), elbowR=(.24, .72, .02), handL=(-.18, .56, .26), handR=(.18, .56, .26))


def _pose(base, **over):
    p = dict(base)
    p.update(over)
    return p


POSES = {
    'seated-warm': (_pose(SEAT, elbowL=(-.2, .78, .22), elbowR=(.2, .78, .22), handL=(-.08, .9, .42), handR=(.08, .9, .42), head=(0, 1.14, -.02)), ('lanternL',)),
    'seated-mug': (_pose(SEAT, elbowR=(.2, .74, .12), handR=(.07, .92, .2), head=(.0, 1.15, -.04)), ('mug',)),
    'seated-lantern': (_pose(SEAT, elbowL=(-.2, .7, .12), elbowR=(.2, .7, .12), handL=(-.05, .62, .3), handR=(.05, .62, .3)), ('lap-lantern',)),
    'seated-radio': (_pose(SEAT, elbowL=(-.24, .76, .15), elbowR=(.24, .76, .15), handL=(-.16, .78, .38), handR=(.16, .8, .38)), ('headset',)),
    'seated-ledger': (_pose(SEAT, elbowL=(-.22, .74, .14), elbowR=(.22, .74, .14), handL=(-.12, .72, .3), handR=(.12, .72, .3), head=(0, 1.13, -.02)), ('ledger', 'cap-rail')),
    'seated-towel': (_pose(SEAT, elbowL=(-.22, .7, .1), elbowR=(.22, .7, .1), handL=(-.05, .6, .3), handR=(.05, .6, .3)), ('towel',)),
    'hatch-lean': (_pose(STAND, neck=(0, 1.4, .12), head=(0, 1.56, .17), elbowL=(-.3, 1.12, .2), elbowR=(.3, 1.12, .2), handL=(-.24, 1.0, .38), handR=(.24, 1.0, .38)), ('vest',)),
    'hatch-clipboard': (_pose(STAND, elbowR=(.2, 1.1, .12), handR=(.1, 1.16, .26), elbowL=(-.2, 1.1, .12), handL=(-.04, 1.2, .28)), ('clipboard', 'coat-long')),
    'hatch-arms': (_pose(STAND, elbowL=(-.21, 1.1, .1), elbowR=(.21, 1.1, .1), handL=(.08, 1.16, .14), handR=(-.08, 1.13, .15)), ('apron-white',)),
    'hatch-welder': (_pose(STAND, elbowR=(.27, 1.1, .08), handR=(.29, .88, .2)), ('welder',)),
    'stand-binoculars': (_pose(STAND, head=(0, 1.61, .04), elbowL=(-.22, 1.36, .16), elbowR=(.22, 1.36, .16), handL=(-.05, 1.58, .2), handR=(.05, 1.58, .2)), ('binoculars', 'beanie')),
    'stand-lantern': (_pose(STAND, elbowR=(.25, 1.1, .04), handR=(.3, .84, .12)), ('hand-lantern', 'beanie')),
    'stand-blanket': (_pose(STAND, elbowL=(-.2, 1.1, .12), elbowR=(.2, 1.1, .12), handL=(-.05, 1.26, .17), handR=(.05, 1.24, .17), head=(0, 1.58, .03)), ('blanket',)),
    'stand-wrench': (_pose(STAND, elbowR=(.3, 1.22, .12), handR=(.3, 1.4, .26), elbowL=(-.25, 1.05, -.05), handL=(-.16, .98, -.05)), ('wrench', 'cap', 'overall')),
    'sentry-rifle': (_pose(STAND, elbowL=(-.3, 1.15, .16), elbowR=(.3, 1.15, .16), handL=(-.26, 1.03, .34), handR=(.26, 1.03, .34)), ('rifle', 'helmet')),
    'sentry-lamp': (_pose(STAND, elbowR=(.24, 1.14, .16), handR=(.16, 1.22, .34), elbowL=(-.24, 1.12, .12), handL=(-.06, 1.18, .32)), ('torch', 'helmet')),
}
# three looks for every pose (coat, trousers, skin, hair; the role's own garments over them)
LOOKS = (('coat-rust', 'trousers-a', 'skin-a', 'hair-a', 'scarf'), ('coat-teal', 'trousers-b', 'skin-b', 'hair-b', None),
         ('coat-olive', 'trousers-c', 'skin-c', 'hair-c', 'scarf'))
ROLE_COAT = {'hatch-clipboard': 'coat-white', 'hatch-arms': 'scrubs', 'seated-ledger': 'coat-maroon', 'stand-wrench': 'overall',
             'seated-towel': 'polo-teal', 'stand-blanket': 'shirt', 'sentry-rifle': 'coat-olive', 'sentry-lamp': 'coat-olive',
             'hatch-welder': 'coat-brown', 'hatch-lean': 'coat-brown'}


def figure(T, pose, variant):
    """A survivor: the pose's joints dressed in its look (Fine: 8-sided limbs; Low: 5)."""
    F_ = finishes()
    j, props = POSES[pose]
    coat, trousers, skin, hair, scarf = LOOKS[variant % 3]
    coat = ROLE_COAT.get(pose, coat) if not (pose.startswith('sentry') and variant == 2) else 'coat-navy'
    fc, ft, fs, fh, fb = F_[coat], F_[trousers], F_[skin], F_[hair], F_['boots']
    sides = 8 if T.detail else 5
    g = Geo()
    P, Nk, H = j['pelvis'], j['neck'], j['head']
    seated = P[1] < .7
    # the torso: hips to shoulders, wider than deep, the jacket's hem flaring a little
    spine = _v(P, Nk)
    L = math.sqrt(sk._dot(spine, spine))
    prof = [(.16, -.04), (.17, .04), (.15, L * .35), (.18, L * .7), (.19, L * .88), (.13, L * .98), (.06, L)]
    sk.lathe(g, prof, P, fc, sides + 2, axis=sk._norm(spine), cap0=True, cap1=True, scale=(.66, 1.0))
    if 'coat-long' in props or 'apron-white' in props:
        apron = F_['coat-white'] if 'apron-white' in props else fc
        if not seated:
            sk.lathe(g, [(.17, -.32), (.165, 0.0)], P, apron, sides + 2, axis=(0, 1, 0), cap0=False, scale=(.7, 1.0))
    if 'vest' in props:
        sk.lathe(g, [(.185, L * .3), (.2, L * .75), (.15, L * .95)], P, F_['apron'], sides + 2, axis=sk._norm(spine), scale=(.7, 1.0))
    if 'overall' in props or 'welder' in props:
        bx(g, T, -.13, .13, P[1] - .05, P[1] + L * .7, P[2] + .1, P[2] + .125, F_['apron'], bevel=.01, cell=0)
    # the neck and head
    limb(g, Nk, _mid(Nk, H, .55), .055, .05, fs, sides)
    hx, hy, hz = H
    mk.ellipsoid(g, H, (.105, .125), fs, sides + 2, rings=5)
    # hair or hat
    if 'helmet' in props:
        sk.lathe(g, [(.135, hy + .02), (.13, hy + .08), (.1, hy + .13), (.0, hy + .15)], (hx, 0, hz - .005), F_['helmet'], sides + 2)
        sk.lathe(g, [(.17, hy + .015), (.135, hy + .03)], (hx, 0, hz - .005), F_['helmet'], sides + 2, cap0=True)
    elif 'beanie' in props or 'cap-rail' in props or 'cap' in props:
        cap = F_['beanie-a' if variant % 2 == 0 else 'beanie-b'] if 'beanie' in props else (F_['coat-maroon'] if 'cap-rail' in props else F_['trousers-c'])
        sk.lathe(g, [(.115, hy + .02), (.112, hy + .07), (.085, hy + .12), (.0, hy + .14)], (hx, 0, hz - .01), cap, sides + 2)
        if 'beanie' not in props:
            bx(g, T, hx - .08, hx + .08, hy + .02, hy + .035, hz + .06, hz + .17, cap, bevel=.005, cell=0)
    else:
        mk.ellipsoid(g, (hx, hy + .02, hz - .012), (.112, .125), fh, sides + 2, rings=4, top=1.0, bottom=-.05)
    if 'welder' in props:
        bx(g, T, hx - .1, hx + .1, hy + .08, hy + .2, hz - .02, hz + .1, F_['metal-dark'], bevel=.01, cell=0)
    # the eyes: two dark marks on the face, toon style
    if T.detail:
        for sx in (-1, 1):
            ex, ey, ez = hx + sx * .036, hy + .012, hz + .1
            bx(g, T, ex - .012, ex + .012, ey - .016, ey + .016, ez - .004, ez + .006, F_['eye'], bevel=0, cell=0)
    if scarf and not seated and 'helmet' not in props:
        sk.lathe(g, [(.075, Nk[1] - .03), (.085, Nk[1] + .02), (.07, Nk[1] + .06)], (Nk[0], 0, Nk[2]), F_[scarf], sides)
    # arms: shoulder, elbow, hand; the camp's orange armband on the left
    for side, s_ in (('L', -1), ('R', 1)):
        sh = _add(Nk, (s_ * .19, -.06, 0))
        el, ha = j['elbow' + side], j['hand' + side]
        sphere(g, sh, .062, fc, sides, 3)
        limb(g, sh, el, .058, .05, fc, sides)
        sphere(g, el, .05, fc, sides, 3)
        limb(g, el, ha, .048, .04, fc, sides)
        d = sk._norm(_v(el, ha))
        mk.ellipsoid(g, _add(ha, d, .04), (.042, .05), fs, sides, rings=3, axis=d)
        if side == 'L':
            a = _mid(sh, el, .45)
            b = _mid(sh, el, .62)
            limb(g, a, b, .062, .058, F_['armband'], sides)
    # legs: hips, knees, ankles, boots
    for side, s_ in (('L', -1), ('R', 1)):
        hp, kn, an = j['hip' + side], j['knee' + side], j['ankle' + side]
        limb(g, hp, kn, .078, .062, ft, sides)
        sphere(g, kn, .062, ft, sides, 3)
        limb(g, kn, an, .058, .046, ft, sides)
        # the boot: forward from the ankle along +z
        bx(g, T, an[0] - .055, an[0] + .055, 0.0, .1, an[2] - .07, an[2] + .17, fb, bevel=.025, cell=0)
    # props
    hl, hr = j['handL'], j['handR']
    if 'mug' in props:
        rod(g, _add(hr, (0, -.02, .03)), _add(hr, (0, .07, .03)), .035, F_['cd-white'], sides)
    if 'lap-lantern' in props:
        hand_lantern(g, T, (hl[0] + hr[0]) / 2, (hl[1] + hr[1]) / 2 - .03, (hl[2] + hr[2]) / 2 + .04, .9)
    if 'lanternL' in props:
        hand_lantern(g, T, -.33, .45, .1, .85)
    if 'hand-lantern' in props:
        hand_lantern(g, T, hr[0], hr[1] - .32, hr[2] + .02, .9)
    if 'headset' in props:
        tube(g, [(hx - .11, hy, hz - .01), (hx - .08, hy + .1, hz - .01), (hx, hy + .14, hz - .01), (hx + .08, hy + .1, hz - .01), (hx + .11, hy, hz - .01)], .009, F_['metal-dark'], 4, caps=False)
        for sx in (-1, 1):
            rod(g, (hx + sx * .1, hy - .01, hz), (hx + sx * .125, hy - .01, hz), .04, F_['metal-dark'], sides)
    if 'ledger' in props:
        c = _mid(hl, hr)
        bx(g, T, c[0] - .17, c[0] + .17, c[1] - .01, c[1] + .015, c[2] - .11, c[2] + .11, F_['paper'], bevel=.004, cell=0)
        bx(g, T, c[0] - .175, c[0] + .175, c[1] - .02, c[1] - .008, c[2] - .115, c[2] + .115, F_['coat-maroon'], bevel=.003, cell=0)
    if 'clipboard' in props:
        bx(g, T, hr[0] - .11, hr[0] + .11, hr[1] - .14, hr[1] + .16, hr[2] + .02, hr[2] + .035, F_['ply'], bevel=.004, cell=0)
        bx(g, T, hr[0] - .09, hr[0] + .09, hr[1] - .12, hr[1] + .12, hr[2] + .035, hr[2] + .04, F_['paper'], bevel=0, cell=0)
    if 'binoculars' in props:
        c = _mid(hl, hr)
        for sx in (-1, 1):
            rod(g, (c[0] + sx * .035, c[1] + .02, c[2] - .03), (c[0] + sx * .035, c[1] + .02, c[2] + .12), .028, F_['metal-dark'], sides)
    if 'towel' in props:
        sk.lathe(g, [(.21, Nk[1] - .3), (.215, Nk[1] - .1), (.16, Nk[1] + .02), (.07, Nk[1] + .05)], (Nk[0], 0, Nk[2] - .01), F_['cd-white'], sides + 2, scale=(.72, 1.0))
    if 'blanket' in props:
        sk.lathe(g, [(.27, .35), (.25, .8), (.22, 1.2), (.17, Nk[1] - .02), (.08, Nk[1] + .06)], (0, 0, -.02), F_['blanket-grey' if variant % 2 else 'blanket-red'], sides + 4, scale=(.78, 1.0), cap0=True)
    if 'wrench' in props:
        d = (0, 1, .2)
        rod(g, hr, _add(hr, sk._norm(d), .3), .018, F_['metal-dark'], 5)
        bx(g, T, hr[0] - .04, hr[0] + .04, hr[1] + .27, hr[1] + .34, hr[2] + .03, hr[2] + .09, F_['metal-dark'], bevel=.005, cell=0)
    if 'rifle' in props:
        # slung across the back, muzzle up over the left shoulder
        a = _add(Nk, (.25, -.62, -.16))
        b = _add(Nk, (-.22, .2, -.15))
        sk_seg = mk.seg
        sk_seg(g, T, a, _mid(a, b, .35), .05, .09, F_['rifle-wood'])
        sk_seg(g, T, _mid(a, b, .35), _mid(a, b, .72), .04, .06, F_['rifle'])
        rod(g, _mid(a, b, .72), b, .012, F_['rifle'], 5)
        tube(g, [_add(Nk, (-.15, -.02, .05)), _add(Nk, (.0, -.25, .14)), _add(Nk, (.2, -.55, .1))], .012, F_['apron'], 4, caps=False)
    if 'torch' in props:
        c = _mid(hl, hr)
        rod(g, (c[0], c[1], c[2] - .05), (c[0], c[1] - .03, c[2] + .2), .035, F_['metal-dark'], sides, r1=.045)
        mk.ellipsoid(g, (c[0], c[1] - .031, c[2] + .205), (.042, .006), glow(FLOOD, 3.0), sides, rings=2, axis=(0, -.15, 1))
    return g
