"""Meridian landmarks: the far layer (art bible 2.2 and 2.5: read from 150 m to 900 m by their emissive parts, fogged at most about
65%, their unlit mass held by the rim; 3,000 triangles or fewer each).

Each dealt instance's landmark stands beyond the Cordon on the camp's line through its door (Core places it, facing the camp: x
across, y up from 12 m below the camp's level, z away from the camp; its front faces -z). They rise high because the city stands
between them and the camp: the Cordon's wall hides their feet, and their read features stand 70 m and more over their base.
  level0     THRESHOLD MUTUAL: a slab tower whose every window is the same flat yellow, its name lit at the top
  poolrooms  MERIDIAN PUBLIC BATHS: a white Deco block under a glass barrel vault lit aqua from within, and its tiled water tower
  wardzero   the Biocontainment slab: white, banded, a QUARANTINE banner, a slow red beacon on its roof mast
  station    Central Station: the train shed's great arched end screen lit arc blue, the clock tower at 6:47
  ironworks  City Ironworks: three stacks burning at their tops over the furnace house's glowing windows
  mast       WMRD 64.7: a guyed lattice mast 168 m tall with its red aircraft lights
"""
import math

import meridian_gates_kit as mk
import meridian_lm_kit as lk
import stagekit as sk
from meridian_gates_kit import F, Geo, glow
from meridian_lm_kit import PI, OPAL, RED_LIGHT

FAMILIES = ('level0', 'poolrooms', 'wardzero', 'station', 'ironworks')


def _box(g, x0, x1, y0, y1, z0, z1, f, skip=('-y',)):
    sk.box(g, x0, x1, y0, y1, z0, z1, f, None, 0.0, cell=0, skip=skip)


def _quad(g, x0, x1, y0, y1, z, f, n=(0, 0, -1)):
    g.face(((x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z)), f, n)


def _letters(g, T, s, x, y, z, cell, f):
    mk.text_stroke(g, s, x, y, z, cell, f, T, style='flat', w=cell * .85, d=.05)


def build(fam, t):
    T = lk.tier('far')
    E = lk.finishes()
    g = Geo()
    {'level0': level0, 'poolrooms': poolrooms, 'wardzero': wardzero, 'station': station, 'ironworks': ironworks}[fam](g, T, E)
    return g


def level0(g, T, E):
    """THRESHOLD MUTUAL: 36 x 18 m, 96 m tall; 26 floors of identical flat-yellow ribbon windows, the name lit at the top."""
    w, d, h = 18.0, 9.0, 96.0
    stucco = F['stucco-beige']
    _box(g, -w, w, 0.0, h, -d, d, stucco)
    yellow = glow(0xF2EE9A, 1.15)
    for k in range(26):
        y0 = 4.0 + k * 3.3
        if y0 + 1.6 > h - 9.0:
            break
        _quad(g, -w + 1.2, w - 1.2, y0, y0 + 1.6, -d - .04, yellow)
        _quad(g, w - 1.2, -w + 1.2, y0, y0 + 1.6, d + .04, yellow, (0, 0, 1))
        g.face(((-w - .04, y0, d - 1.0), (-w - .04, y0, -d + 1.0), (-w - .04, y0 + 1.6, -d + 1.0), (-w - .04, y0 + 1.6, d - 1.0)), yellow, (-1, 0, 0))
        g.face(((w + .04, y0, -d + 1.0), (w + .04, y0, d - 1.0), (w + .04, y0 + 1.6, d - 1.0), (w + .04, y0 + 1.6, -d + 1.0)), yellow, (1, 0, 0))
    _box(g, -w - .4, w + .4, h - .6, h, -d - .4, d + .4, F['alu'])
    _quad(g, -w + .5, w - .5, h - 8.0, h - 1.0, -d - .05, F['alu-dark'])
    _letters(g, T, 'THRESHOLD MUTUAL', 0.0, h - 6.6, -d - .1, .55, glow(0xF2EE9A, 2.6))


def poolrooms(g, T, E):
    """MERIDIAN PUBLIC BATHS: a 52 x 30 m white tiled block 52 m tall, stepped at the top, under a glass barrel vault lit aqua;
    BATHS in tall letters; the tiled water tower beside it with its glowing tank."""
    white, teal = F['tile-pool'], F['tile-teal']
    _box(g, -26.0, 26.0, 0.0, 52.0, -15.0, 15.0, white)
    for k, y in enumerate((52.0, 53.5, 55.0)):
        _box(g, -26.0 + k * 2.0, 26.0 - k * 2.0, y, y + 1.5, -15.0 + k * 1.2, 15.0 - k * 1.2, F['coping'])
    _quad(g, -26.0, 26.0, 40.0, 42.0, -15.05, teal)
    # the vault: a half cylinder of lit glass along x, ribbed
    n = 12
    R, cy = 11.0, 56.5
    aqua = glow(0x52E4E0, 1.5)
    for i in range(n):
        a0, a1 = PI * i / n, PI * (i + 1) / n
        p0 = (R * math.cos(a0), cy + R * math.sin(a0))
        p1 = (R * math.cos(a1), cy + R * math.sin(a1))
        nn = mk._norm((math.cos((a0 + a1) / 2), math.sin((a0 + a1) / 2), 0))
        g.face(((-20.0, p0[1], p0[0]), (-20.0, p1[1], p1[0]), (20.0, p1[1], p1[0]), (20.0, p0[1], p0[0])), aqua, (0, nn[1], nn[0]))
    for x in (-20.0, 20.0):
        g.face([(x, cy + R * math.sin(PI * i / n), R * math.cos(PI * i / n)) for i in range(n + 1)], glow(0x9EEDE6, 1.2), (-1 if x < 0 else 1, 0, 0))
    for x in range(-20, 21, 5):
        for i in range(n):
            a0, a1 = PI * i / n, PI * (i + 1) / n
            mk.seg(g, T, (x, cy + R * math.sin(a0), R * math.cos(a0)), (x, cy + R * math.sin(a1), R * math.cos(a1)), .4, .4, F['iron-black'])
    _letters(g, T, 'BATHS', 0.0, 44.0, -15.1, 1.0, glow(0x52E4E0, 2.6))
    for k in range(5):
        x = -20.0 + k * 10.0
        _quad(g, x - 2.0, x + 2.0, 8.0, 36.0, -15.05, glow(0x2AB4F2, .9))
    # the water tower: a tiled shaft and its glowing tank
    tx, tz = 21.0, 4.0
    _box(g, tx - 4.0, tx + 4.0, 52.0, 72.0, tz - 4.0, tz + 4.0, white)
    _quad(g, tx - 1.0, tx + 1.0, 54.0, 70.0, tz - 4.05, teal)
    sk.lathe(g, [(5.0, 72.0), (5.4, 73.0), (5.4, 83.0), (4.6, 84.0), (1.0, 86.0)], (tx, 0.0, tz), glow(0x52E4E0, 1.8), 10, smooth=False)


def wardzero(g, T, E):
    """The Biocontainment slab: 44 x 16 m, 92 m tall, white with teal bands; a QUARANTINE banner hung on its face; a mast on the
    roof with the slow red beacon; the hexagon mark."""
    w, d, h = 22.0, 8.0, 92.0
    white = E['white-cladding-far']
    _box(g, -w, w, 0.0, h, -d, d, white)
    for y in range(10, int(h), 10):
        _quad(g, -w, w, y, y + .8, -d - .03, F['cladding-teal'])
    for k in range(20):
        y = 6.0 + k * 4.2
        if y > h - 30:
            break
        _quad(g, -w + 2.0, w - 2.0, y + 1.2, y + 2.4, -d - .02, glow(0xD4E6FF, .55) if k % 3 else F['glass-dark'])
    # the banner: a white sheet with QUARANTINE in red, lit by floods from below
    _quad(g, -15.0, 15.0, 64.0, 82.0, -d - .4, glow(0xE8F2FF, .7))
    _letters(g, T, 'QUARANTINE', 0.0, 70.0, -d - .5, .45, glow(0xFF2A30, 2.4))
    n = 6
    g.face([(math.cos(PI / 3 * i) * 4.0, 88.0 - 6.0 + math.sin(PI / 3 * i) * 4.0, -d - .05) for i in range(n)], F['cladding-teal'], (0, 0, -1))
    # the roof mast and the beacon
    mk.rod(g, (0.0, h, 0.0), (0.0, h + 6.0, 0.0), .35, F['steel-dark'], 6)
    mk.ellipsoid(g, (0.0, h + 7.0, 0.0), (2.0, 2.0), glow(0xFF2A30, 3.4), 8, rings=4)


def station(g, T, E):
    """Central Station: the train shed's great arched end screen (56 m wide, its crest 45 m up) lit arc blue under the barrel of
    its roof, and the clock tower in front of it at 6:47, 88 m."""
    n = 12
    R, Wd = 28.0, 40.0
    cy = 17.0
    glass = glow(0x7CB4FF, 1.0)
    # the shed's roof: a half barrel along z
    for i in range(n):
        a0, a1 = PI * i / n, PI * (i + 1) / n
        p0 = (R * math.cos(a0), cy + R * math.sin(a0))
        p1 = (R * math.cos(a1), cy + R * math.sin(a1))
        nn = (math.cos((a0 + a1) / 2), math.sin((a0 + a1) / 2))
        g.face(((p0[0], p0[1], 0.0), (p0[0], p0[1], Wd), (p1[0], p1[1], Wd), (p1[0], p1[1], 0.0)), F['slate'], (nn[0], nn[1], 0))
    # the end screen: lit glass in an iron arch, mullions
    g.face([(R * math.cos(PI * i / n), cy + R * math.sin(PI * i / n), -.1) for i in range(n + 1)][::-1], glass, (0, 0, -1))
    for i in range(n):
        a0, a1 = PI * i / n, PI * (i + 1) / n
        mk.seg(g, T, (R * math.cos(a0), cy + R * math.sin(a0), -.3), (R * math.cos(a1), cy + R * math.sin(a1), -.3), 1.2, .8, F['iron-black'])
    for x in range(-24, 25, 6):
        top = cy + math.sqrt(max(0.0, R * R - x * x))
        mk.seg(g, T, (x, cy, -.25), (x, top, -.25), .35, .3, F['iron-black'])
    _box(g, -R - 3.0, R + 3.0, 0.0, cy, -2.0, Wd, F['brick-stock'])
    for k in range(9):
        x = -26.0 + k * 6.5
        _quad(g, x - 1.2, x + 1.2, 4.0, 12.0, -2.05, glow(0xFFB062, 1.0))
    _letters(g, T, 'CENTRAL STATION', 0.0, 12.8, -2.1, .45, glow(0xF2E6C6, 1.8))
    # the clock tower in front, the dial on its front at 80 m
    tz0, tz1 = -16.0, -8.0
    _box(g, -4.0, 4.0, 0.0, 84.0, tz0, tz1, F['brick-stock'])
    _box(g, -4.6, 4.6, 74.0, 86.0, tz0 - .6, tz1 + .6, F['stone-dress'])
    for (n_, c) in (((0, 0, -1), (0.0, 80.0, tz0 - .65)), ((-1, 0, 0), (-4.65, 80.0, (tz0 + tz1) / 2)), ((1, 0, 0), (4.65, 80.0, (tz0 + tz1) / 2))):
        mk.disc(g, c[0], c[1], c[2], 3.5, 12, glow(OPAL, 2.0), normal=n_)
    sub = Geo()
    mk.clock(sub, T, 3.5, glow(OPAL, 2.0), F['brass'], F['iron-black'], F['iron-black'], roman=False, depth=.05, rim=.2, ticks=False)
    mk.part(g, sub, (0, 0, 0, 1), (0.0, 80.0, tz0 - .62))
    sk.lathe(g, [(4.6, 86.0), (3.0, 89.0), (.4, 92.0)], (0.0, 0.0, (tz0 + tz1) / 2), F['slate'], 4, smooth=False, phase=PI / 4)


def ironworks(g, T, E):
    """City Ironworks: the furnace house (40 x 30 m, 35 m) with its arched windows glowing, and three stacks burning at their
    tops at 86, 94 and 89 m; a gantry crane's silhouette on its roof."""
    _box(g, -20.0, 20.0, 0.0, 35.0, -15.0, 15.0, F['brick-buff'])
    for k in range(7):
        x = -16.5 + k * 5.5
        g.face(((x - 1.6, 14.0, -15.05), (x + 1.6, 14.0, -15.05), (x + 1.6, 26.0, -15.05), (x, 28.0, -15.05), (x - 1.6, 26.0, -15.05)), glow(0xFF6418, 1.6), (0, 0, -1))
    g.face(((-20.0, 35.0, -15.0), (20.0, 35.0, -15.0), (20.0, 44.0, 0.0), (-20.0, 44.0, 0.0)), F['slate'], (0, .86, -.5))
    g.face(((20.0, 35.0, 15.0), (-20.0, 35.0, 15.0), (-20.0, 44.0, 0.0), (20.0, 44.0, 0.0)), F['slate'], (0, .86, .5))
    _letters(g, T, 'CITY IRONWORKS', 0.0, 30.0, -15.1, .4, glow(0xFFAE3C, 1.6))
    for (x, top) in ((-10.0, 89.0), (0.0, 94.0), (10.0, 86.0)):
        sk.lathe(g, [(2.6, 30.0), (2.2, top - 2.0), (2.5, top - 1.2), (2.5, top)], (x, 0.0, 6.0), F['brick-soot'], 8, smooth=False)
        sk.lathe(g, [(2.1, top), (2.4, top + 2.5), (1.4, top + 5.0), (.2, top + 7.0)], (x, 0.0, 6.0), glow(0xFF6418, 3.0), 8, smooth=False)
        sk.lathe(g, [(1.4, top + 1.0), (1.0, top + 4.0), (.1, top + 5.5)], (x, 0.0, 6.0), glow(0xFFF0C4, 3.4), 6, smooth=False)
    lk.lattice(g, T, (-18.0, 46.0, -6.0), (18.0, 46.0, -6.0), (0, 1, 0), 2.5, F['iron-raw'], bays=8, w=.6)


def mast(t):
    """WMRD 64.7: a triangular lattice mast 168 m tall on its hut, guyed at three heights, red aircraft lights at 60, 112 and 168 m
    (big, so they read at 500 m), and the station's call sign on the hut."""
    T = lk.tier('far')
    E = lk.finishes()
    g = Geo()
    iron = E['iron-lattice']
    r = 1.6
    legs = [(r * math.cos(2 * PI * k / 3), r * math.sin(2 * PI * k / 3)) for k in range(3)]
    H = 168.0
    for (x, z) in legs:
        mk.seg(g, T, (x, 0.0, z), (x * .6, H, z * .6), .35, .35, iron)
    for k in range(int(H / 6)):
        y0, y1 = k * 6.0, (k + 1) * 6.0
        s0, s1 = 1 - .4 * y0 / H, 1 - .4 * y1 / H
        for j in range(3):
            a, b = legs[j], legs[(j + 1) % 3]
            mk.seg(g, T, (a[0] * s0, y0, a[1] * s0), (b[0] * s1, y1, b[1] * s1), .14, .14, iron)
    for (y, rr) in ((60.0, 3.0), (112.0, 3.0), (168.0, 3.6)):
        mk.ellipsoid(g, (0.0, y + rr * .6, 0.0), (rr * .55, rr * .55), glow(RED_LIGHT, 3.4), 8, rings=4)
    for y in (56.0, 108.0, 160.0):
        for k in range(3):
            a = 2 * PI * k / 3 + PI / 3
            mk.rod(g, (0.0, y, 0.0), (math.cos(a) * 30.0, 0.0, math.sin(a) * 30.0), .06, F['steel-dark'], 3, caps=False)
    _box(g, -5.0, 5.0, 0.0, 4.0, -4.0, 4.0, F['concrete'])
    _letters(g, T, 'WMRD 64.7', 0.0, 1.2, -4.1, .18, glow(0xFF8A3A, 1.6))
    return g
