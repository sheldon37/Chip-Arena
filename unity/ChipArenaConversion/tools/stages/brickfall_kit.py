"""Brickfall's stage art: the finishes and the shapes its scripts share (docs/arenas/BRICKFALL-ART.md, docs/arenas/STAGE-ART.md).
Run inside Blender through brickfall_art.py. Everything is in the game's space (Unity: metres, +y up).

A `Frame` is a wall's own space: a point `o`, `u` along the wall (horizontal), `v` up and `n` out of it (n = u x v, so a frame is
never mirrored and a box built in it keeps its faces' winding). Walls are faces with their openings cut out (`wallface`), each
opening with its reveal, sill, head and what fills it (glass with bars, boards, brick, a shutter, a door); mouldings are
profiles run along a wall (`mould`); lettering is a 5 x 7 face of raised strokes (`text`)."""
import math
import random

import stagekit as sk
from stagekit import Geo, _add, _norm, _sub, _cross

# the look's palette (Core NativeBrickfallScenery, ARENA_PROPOSAL 3.10)
BRICK_RED, BRICK_DARK, MORTAR, BUFF, TERRACOTTA, CONCRETE, CONCRETE_DARK = 0x9C4A30, 0x7E3A26, 0xCDBB9E, 0xB8774E, 0xE3C9A0, 0x8F8A80, 0x5F5A53
GREEN, RUST, TIMBER, ASPHALT, COBBLE, TAR, YELLOW, BLACK, DANGER = 0x3E6B5A, 0x7A3E22, 0xB4834F, 0x3A3836, 0x6B625A, 0x2B2A2E, 0xF4B321, 0x1C1A18, 0xD6332A
GLASS, LIT, DARK_GLASS, IRON, CORRUGATED = 0x6E8F96, 0xFFB45E, 0x1F2A30, 0x2A2624, 0x7D8478

F = {}
STATS = {}


def tally(g, key, mark):
    STATS[key] = STATS.get(key, 0) + g.faces - mark


def finishes():
    """The stage's finishes, by name in F."""
    def add(name, *a, **k):
        F[name] = sk.fin('bf-' + name, *a, **k)
    B = sk.DETAIL_BRICK
    add('brick', BRICK_RED, B, smooth=.12, dirt=1.0, moss=.25, wear=.25)
    add('brick-dark', BRICK_DARK, B, smooth=.12, dirt=1.0, moss=.25, wear=.25)
    add('brick-buff', BUFF, B, smooth=.12, dirt=.9, moss=.2, wear=.25)
    add('brick-plum', 0x6E3B2E, B, smooth=.12, dirt=1.0, moss=.25, wear=.2)
    add('brick-rose', 0xA0573A, B, smooth=.12, dirt=1.0, moss=.2, wear=.25)
    add('brick-tan', 0x9A6248, B, smooth=.12, dirt=1.0, moss=.2, wear=.25)
    add('brick-fresh', 0xC2734E, B, smooth=.1, dirt=.3, wear=.1)             # a broken face: the brick's unweathered heart
    add('brick-soot', 0x4A3A34, B, smooth=.1, dirt=.6)
    add('stone', TERRACOTTA, sk.DETAIL_CARVED, smooth=.22, dirt=.8, moss=.2, wear=.3)
    add('stone-dark', 0xB79E7C, sk.DETAIL_CARVED, smooth=.2, dirt=.9, moss=.3, wear=.25)
    add('mortar', MORTAR, sk.DETAIL_PLASTER, smooth=.1, dirt=.8)
    add('concrete', CONCRETE, sk.DETAIL_PLASTER, smooth=.16, dirt=1.0, moss=.15, wear=.2)
    add('concrete-dark', CONCRETE_DARK, sk.DETAIL_PLASTER, smooth=.14, dirt=1.0, moss=.15, wear=.2)
    add('concrete-fresh', 0xC9C2B4, sk.DETAIL_PLASTER, smooth=.14, dirt=.3)
    add('plaster', 0xD8C8A8, sk.DETAIL_PLASTER, smooth=.1, dirt=.9)
    add('paper-rose', 0xB07A7A, sk.DETAIL_PLASTER, smooth=.1, dirt=.7)
    add('paper-sage', 0x8C9A78, sk.DETAIL_PLASTER, smooth=.1, dirt=.7)
    add('paper-ochre', 0xC49A48, sk.DETAIL_PLASTER, smooth=.1, dirt=.7)
    add('paper-blue', 0x7C8EA0, sk.DETAIL_PLASTER, smooth=.1, dirt=.7)
    add('green', GREEN, sk.DETAIL_IRON, smooth=.45, metal=.35, dirt=.7, wear=.5)
    add('green-paint', GREEN, sk.DETAIL_NONE, smooth=.4, dirt=.8, wear=.45)
    add('iron', IRON, sk.DETAIL_IRON, smooth=.4, metal=.5, dirt=.6, wear=.5)
    add('steel', 0x8A9296, sk.DETAIL_IRON, smooth=.55, metal=.5, dirt=.6, wear=.4)
    add('rust', RUST, sk.DETAIL_IRON, smooth=.25, metal=.3, dirt=.8, wear=.2)
    add('corrugated', CORRUGATED, sk.DETAIL_NONE, smooth=.4, metal=.3, dirt=.9, wear=.3)
    add('white-paint', 0xD9D4C6, sk.DETAIL_NONE, smooth=.35, dirt=.9, wear=.3)
    add('cab-red', 0xA8392C, sk.DETAIL_NONE, smooth=.6, metal=.2, dirt=.7, wear=.4)
    add('timber', TIMBER, sk.DETAIL_WOOD, smooth=.25, dirt=.8, wear=.3, param=0)
    add('timber-y', TIMBER, sk.DETAIL_WOOD, smooth=.25, dirt=.8, wear=.3, param=1)
    add('timber-z', TIMBER, sk.DETAIL_WOOD, smooth=.25, dirt=.8, wear=.3, param=2)
    add('timber-dark', 0x6E4A2E, sk.DETAIL_WOOD, smooth=.3, dirt=.8, wear=.3, param=1)
    add('timber-dark-x', 0x6E4A2E, sk.DETAIL_WOOD, smooth=.3, dirt=.8, wear=.3, param=0)
    add('timber-dark-z', 0x6E4A2E, sk.DETAIL_WOOD, smooth=.3, dirt=.8, wear=.3, param=2)
    add('ply', 0xC9A56E, sk.DETAIL_WOOD, smooth=.2, dirt=.7, param=1)
    add('floorboard-x', 0x8A6240, sk.DETAIL_WOOD, smooth=.35, dirt=.8, wear=.2, param=0)
    add('floorboard-z', 0x8A6240, sk.DETAIL_WOOD, smooth=.35, dirt=.8, wear=.2, param=2)
    add('tar', TAR, sk.DETAIL_CARVED, smooth=.3, dirt=.8)
    add('asphalt', ASPHALT, sk.DETAIL_CARVED, smooth=.22, dirt=.9)
    add('asphalt-patch', 0x2E2D2C, sk.DETAIL_CARVED, smooth=.3, dirt=.6)
    add('cobble', COBBLE, sk.DETAIL_FLAG, smooth=.3, dirt=1.0, moss=.3, param=.22)
    add('paving', 0x8C857A, sk.DETAIL_FLAG, smooth=.25, dirt=1.0, moss=.25, param=.55)
    add('kerb', 0x9A948A, sk.DETAIL_CARVED, smooth=.25, dirt=.9, wear=.3)
    add('lane', 0xE8D9A8, sk.DETAIL_NONE, smooth=.2, dirt=1.0)
    add('yellow', YELLOW, sk.DETAIL_NONE, smooth=.4, dirt=.5, wear=.3)
    add('black', BLACK, sk.DETAIL_NONE, smooth=.4, dirt=.4, wear=.3)
    add('danger', DANGER, sk.DETAIL_NONE, smooth=.4, dirt=.4, wear=.2)
    add('glass', DARK_GLASS, sk.DETAIL_NONE, smooth=.9, metal=.1, dirt=.35)
    add('glass-pale', GLASS, sk.DETAIL_NONE, smooth=.85, metal=.1, dirt=.5)
    add('lit', LIT, glow=1.5)
    add('lit-dim', 0xE89A52, glow=.8)
    add('lit-cool', 0x9FC4D8, glow=.9)
    add('lamp', 0xFFFFFF, glow=1.0)                 # the band lamp's lens: the runtime's glow gives it its colour
    add('sodium', 0xFF9A3C, glow=2.6)
    add('neon', 0x4FA8FF, glow=2.2)
    add('coral', 0xFF5A4E, glow=1.8)
    add('fire', 0xFF7A2E, glow=2.4)
    add('burlap', 0x9C8B6E, sk.DETAIL_CLOTH, smooth=.1, dirt=.9)
    add('canvas-red', 0xB8483E, sk.DETAIL_CLOTH, smooth=.12, dirt=.8)
    add('canvas-green', 0x3E6B5A, sk.DETAIL_CLOTH, smooth=.12, dirt=.8)
    add('canvas-cream', 0xE3C9A0, sk.DETAIL_CLOTH, smooth=.12, dirt=.8)
    add('tarp', 0x4F6F8A, sk.DETAIL_CLOTH, smooth=.25, dirt=.7)
    add('slate', 0x4A4E57, sk.DETAIL_TILE, smooth=.35, dirt=.9, moss=.2, param=.3)
    add('rubber', 0x1E1D1C, sk.DETAIL_NONE, smooth=.25, dirt=.5)
    add('poster-a', 0xD9B24A, sk.DETAIL_NONE, smooth=.2, dirt=.9)
    add('poster-b', 0x5F8FA3, sk.DETAIL_NONE, smooth=.2, dirt=.9)
    add('poster-c', 0xC8553F, sk.DETAIL_NONE, smooth=.2, dirt=.9)
    add('poster-d', 0xE6DCC2, sk.DETAIL_NONE, smooth=.2, dirt=.9)
    add('bronze', 0x6A5A3A, sk.DETAIL_BRASS, smooth=.5, metal=.6, dirt=.6, wear=.4)
    add('dust', 0xB8A68F, sk.DETAIL_PLASTER, smooth=.08, dirt=.5)
    add('rubble', 0x8E6A58, sk.DETAIL_CARVED, smooth=.1, dirt=.7)
    return F


def look_fin(look, color=0):
    """A block look (Core NativeBlockLooks.Keys) as a finish."""
    return F[{'brick-red': 'brick', 'brick-buff': 'brick-buff', 'terracotta': 'stone', 'concrete-board': 'concrete', 'steel-green': 'green',
              'timber': 'timber', 'tar': 'tar', 'asphalt': 'asphalt', 'cobble': 'cobble', 'corrugated': 'corrugated', 'plain': 'burlap'}.get(look, 'concrete')]


# ---------------------------------------------------------------------------------------------------------------------------
# Frames
# ---------------------------------------------------------------------------------------------------------------------------
class Frame:
    """A wall's space: o + a u + b v + c n (u along the wall, v up, n out; n = u x v)."""

    def __init__(self, o, u, v=(0.0, 1.0, 0.0)):
        self.o = tuple(float(x) for x in o)
        self.u = _norm(u)
        self.v = _norm(v)
        self.n = _cross(self.u, self.v)

    def P(self, a, b, c=0.0):
        o, u, v, n = self.o, self.u, self.v, self.n
        return (o[0] + a * u[0] + b * v[0] + c * n[0], o[1] + a * u[1] + b * v[1] + c * n[1], o[2] + a * u[2] + b * v[2] + c * n[2])

    def D(self, a, b, c):
        u, v, n = self.u, self.v, self.n
        return (a * u[0] + b * v[0] + c * n[0], a * u[1] + b * v[1] + c * n[1], a * u[2] + b * v[2] + c * n[2])

    def rows(self):
        u, v, n, o = self.u, self.v, self.n, self.o
        return [u[0], v[0], n[0], o[0], u[1], v[1], n[1], o[1], u[2], v[2], n[2], o[2]]

    def shifted(self, a=0.0, b=0.0, c=0.0):
        return Frame(self.P(a, b, c), self.u, self.v)


def wall_frame(ax, az, bx, bz, y=0.0):
    """The wall from (ax, az) to (bx, bz) at height y; it faces to the right of that walk seen from above with +z up the page
    reversed: n = u x up."""
    return Frame((ax, y, az), (bx - ax, 0.0, bz - az))


def facing(nx, nz, x, z, y=0.0):
    """A frame at (x, y, z) whose n is (nx, 0, nz): u = (nz, 0, -nx)."""
    return Frame((x, y, z), (nz, 0.0, -nx))


def fbox(g, T, fr, u0, u1, v0, v1, c0, c1, f, bevel=.012, cell=0, skip=()):
    """A box in a frame's coordinates (skip: '-x' u0, '+x' u1, '-y' v0, '+y' v1, '-z' c0, '+z' c1)."""
    t = Geo()
    sk.box(t, u0, u1, v0, v1, c0, c1, f, T, bevel, cell=cell, skip=skip)
    g.extend(t.transformed(fr.rows()))


def fgeo(g, fr, t):
    g.extend(t.transformed(fr.rows()))


# ---------------------------------------------------------------------------------------------------------------------------
# Walls with openings
# ---------------------------------------------------------------------------------------------------------------------------
class Hole:
    """An opening in a wall face: u0..u1, v0..v1 to the springing, `rise` the arch over it (0: a flat head)."""

    def __init__(self, u0, u1, v0, v1, rise=0.0, fill='glass', depth=.14, sill=True, head='arch', lit=None, bars=(2, 3)):
        self.u0, self.u1, self.v0, self.v1, self.rise = u0, u1, v0, v1, rise
        self.fill, self.depth, self.sill, self.head, self.lit, self.bars = fill, depth, sill, head, lit, bars

    def top(self, u):
        if self.rise <= 0:
            return self.v1
        L = (self.u1 - self.u0) / 2
        return self.v1 + sk.arch_height(L, self.rise, min(u - self.u0, self.u1 - u))

    def cuts(self, T):
        if self.rise <= 0:
            return [self.u0, self.u1]
        n = (10, 6, 4)[T.i]
        return [self.u0 + (self.u1 - self.u0) * i / n for i in range(n + 1)]


def wallface(g, T, fr, w, h, f, holes=(), c=0.0, cell=None, u0=0.0, v0=0.0):
    """The wall's face from (u0, v0) to (w, h) at depth c, its openings left out."""
    cs = T.cell if cell is None else cell
    us = sorted(set([u0, w] + [x for hl in holes for x in (hl.u0, hl.u1) if u0 < x < w]))
    vs = sorted(set([v0, h] + [x for hl in holes for x in (hl.v0, hl.v1 + hl.rise) if v0 < x < h]))
    n = fr.n
    for i in range(len(us) - 1):
        ua, ub = us[i], us[i + 1]
        if ub - ua < 1e-5:
            continue
        um = (ua + ub) / 2
        for j in range(len(vs) - 1):
            va, vb = vs[j], vs[j + 1]
            if vb - va < 1e-5:
                continue
            vm = (va + vb) / 2
            if any(hl.u0 < um < hl.u1 and hl.v0 < vm < hl.v1 + hl.rise for hl in holes):
                continue
            nu = max(1, int(math.ceil((ub - ua) / cs)))
            nv = max(1, int(math.ceil((vb - va) / cs)))
            for a in range(nu):
                for b in range(nv):
                    x0, x1 = ua + (ub - ua) * a / nu, ua + (ub - ua) * (a + 1) / nu
                    y0, y1 = va + (vb - va) * b / nv, va + (vb - va) * (b + 1) / nv
                    g.face((fr.P(x0, y0, c), fr.P(x1, y0, c), fr.P(x1, y1, c), fr.P(x0, y1, c)), f, n)
    for hl in holes:
        if hl.rise <= 0:
            continue
        cu = hl.cuts(T)
        vt = hl.v1 + hl.rise
        for k in range(len(cu) - 1):
            a, b = cu[k], cu[k + 1]
            g.face((fr.P(a, hl.top(a), c), fr.P(b, hl.top(b), c), fr.P(b, vt, c), fr.P(a, vt, c)), f, n)


def opening(g, T, fr, hl, f_wall, c=0.0, style=None):
    """An opening's reveal (jambs, sill, head), what fills it at its depth, and its dressings: a stone sill, and over it a brick
    arch ring or a stone lintel. style: {'trim': finish of sill and lintel, 'ring': finish of the arch ring}."""
    style = style or {}
    d = hl.depth
    u0, u1, v0, v1 = hl.u0, hl.u1, hl.v0, hl.v1
    back = c - d
    n = fr.n
    um = fr.u
    # jambs and sill
    g.face((fr.P(u0, v0, c), fr.P(u0, v0, back), fr.P(u0, v1, back), fr.P(u0, v1, c)), f_wall, um)
    g.face((fr.P(u1, v0, c), fr.P(u1, v0, back), fr.P(u1, v1, back), fr.P(u1, v1, c)), f_wall, (-um[0], -um[1], -um[2]))
    g.face((fr.P(u0, v0, c), fr.P(u1, v0, c), fr.P(u1, v0, back), fr.P(u0, v0, back)), f_wall, fr.v)
    cu = hl.cuts(T)
    # the head's soffit
    for k in range(len(cu) - 1):
        a, b = cu[k], cu[k + 1]
        g.face((fr.P(a, hl.top(a), c), fr.P(b, hl.top(b), c), fr.P(b, hl.top(b), back), fr.P(a, hl.top(a), back)), f_wall, (0, -1, 0))
    fill = hl.fill
    if fill == 'none':
        pass
    elif fill in ('glass', 'lit'):
        fg = hl.lit if (fill == 'lit' and hl.lit is not None) else F['glass']
        for k in range(len(cu) - 1):
            a, b = cu[k], cu[k + 1]
            g.face((fr.P(a, v0, back), fr.P(b, v0, back), fr.P(b, hl.top(b), back), fr.P(a, hl.top(a), back)), fg, n)
        # the sash: a frame round the pane and its glazing bars
        if T.detail:
            fb = style.get('frame', F['white-paint'])
            t = .045
            fbox(g, T, fr, u0, u0 + t, v0, v1, back, back + .035, fb, 0)
            fbox(g, T, fr, u1 - t, u1, v0, v1, back, back + .035, fb, 0)
            fbox(g, T, fr, u0 + t, u1 - t, v0, v0 + t, back, back + .035, fb, 0)
            nu, nv = hl.bars
            for i in range(1, nu):
                x = u0 + (u1 - u0) * i / nu
                fbox(g, T, fr, x - .02, x + .02, v0 + t, hl.top(x), back, back + .03, fb, 0)
            for j in range(1, nv):
                y = v0 + (v1 + hl.rise * .0 - v0) * j / nv
                fbox(g, T, fr, u0 + t, u1 - t, y - .02, y + .02, back, back + .03, fb, 0)
    elif fill == 'board':
        fb = F['ply']
        for k in range(len(cu) - 1):
            a, b = cu[k], cu[k + 1]
            g.face((fr.P(a, v0, back + .05), fr.P(b, v0, back + .05), fr.P(b, hl.top(b), back + .05), fr.P(a, hl.top(a), back + .05)), fb, n)
        if T.detail:
            for y in (v0 + (v1 - v0) * .28, v0 + (v1 - v0) * .72):
                fbox(g, T, fr, u0 + .02, u1 - .02, y - .06, y + .06, back + .05, back + .085, F['timber-dark-x'], .008)
    elif fill == 'brick':
        fb = style.get('blind', F['brick-dark'])
        for k in range(len(cu) - 1):
            a, b = cu[k], cu[k + 1]
            g.face((fr.P(a, v0, back), fr.P(b, v0, back), fr.P(b, hl.top(b), back), fr.P(a, hl.top(a), back)), fb, n)
    elif fill == 'shutter':
        # a roller shutter: slats as a shallow saw profile
        slat = (.11, .22, .44)[T.i]
        k = max(1, int((v1 + hl.rise - v0) / slat))
        fs = F['corrugated']
        for i in range(k):
            ya, yb = v0 + (v1 + hl.rise - v0) * i / k, v0 + (v1 + hl.rise - v0) * (i + 1) / k
            ym = (ya + yb) / 2
            g.face((fr.P(u0, ya, back), fr.P(u1, ya, back), fr.P(u1, ym, back + .022), fr.P(u0, ym, back + .022)), fs)
            g.face((fr.P(u0, ym, back + .022), fr.P(u1, ym, back + .022), fr.P(u1, yb, back), fr.P(u0, yb, back)), fs)
        if T.detail:
            fbox(g, T, fr, u0, u1, v0, v0 + .08, back, back + .06, F['iron'], .008)
    elif fill == 'door':
        fd = style.get('door', F['timber-dark'])
        for k in range(len(cu) - 1):
            a, b = cu[k], cu[k + 1]
            g.face((fr.P(a, v0, back), fr.P(b, v0, back), fr.P(b, hl.top(b), back), fr.P(a, hl.top(a), back)), fd, n)
        if T.detail:
            w = u1 - u0
            for (a, b) in ((.12, .46), (.54, .88)):
                for (p, q) in ((.08, .42), (.5, .9)):
                    fbox(g, T, fr, u0 + w * a, u0 + w * b, v0 + (v1 - v0) * p, v0 + (v1 - v0) * q, back, back + .022, fd, .01)
            fbox(g, T, fr, u0 + w * .5 - .015, u0 + w * .5 + .015, v0, v1, back, back + .03, F['timber-dark'], 0)
            sk.cylinder(g, fr.P(u0 + w * .56, v0 + 1.0, back), fr.P(u0 + w * .56, v0 + 1.0, back + .07), .028, F['bronze'], T.small)
    trim = style.get('trim', F['stone'])
    if hl.sill and trim is not None:
        fbox(g, T, fr, u0 - .08, u1 + .08, v0 - .1, v0, c - d * .6, c + .06, trim, .015)
    if hl.head == 'arch' and hl.rise > 0:
        ring = style.get('ring', f_wall)
        arch_ring(g, T, fr, hl, ring, c, .24, .035)
        if T.detail and style.get('key', True) and (trim is not None or style.get('ring') is not None):
            um_ = (u0 + u1) / 2
            fbox(g, T, fr, um_ - .11, um_ + .11, v1 + hl.rise - .02, v1 + hl.rise + .3, c, c + .06, trim if trim is not None else style['ring'], .012)
    elif hl.head == 'lintel' and trim is not None:
        fbox(g, T, fr, u0 - .14, u1 + .14, v1 + hl.rise, v1 + hl.rise + .22, c - .02, c + .045, trim, .015)
    elif hl.head == 'steel':
        fbox(g, T, fr, u0 - .2, u1 + .2, v1 + hl.rise, v1 + hl.rise + .2, c - .02, c + .04, F['iron'], .01)


def arch_ring(g, T, fr, hl, f, c, deep, proud):
    """A ring of brick on edge over an arched opening: a band `deep` wide standing `proud` of the wall."""
    cu = hl.cuts(T)
    L = (hl.u1 - hl.u0) / 2
    um = (hl.u0 + hl.u1) / 2
    inner, outer = [], []
    for a in cu:
        y = hl.top(a)
        # outward from the arch's centre line (approximate normal of the curve)
        e = 1e-3
        ya, yb = hl.top(max(hl.u0, a - e)), hl.top(min(hl.u1, a + e))
        dx, dy = (min(hl.u1, a + e) - max(hl.u0, a - e)), yb - ya
        l = math.hypot(dx, dy) or 1
        nx, ny = -dy / l, dx / l
        inner.append((a, y))
        outer.append((a + nx * deep, y + ny * deep))
    n = fr.n
    for k in range(len(cu) - 1):
        (a0, b0), (a1, b1), (c1, d1), (c0, d0) = inner[k], inner[k + 1], outer[k + 1], outer[k]
        g.face((fr.P(a0, b0, c + proud), fr.P(a1, b1, c + proud), fr.P(c1, d1, c + proud), fr.P(c0, d0, c + proud)), f, n)
        # the ring's outer edge back to the wall
        g.face((fr.P(c0, d0, c + proud), fr.P(c1, d1, c + proud), fr.P(c1, d1, c), fr.P(c0, d0, c)), f, fr.D(c0 + c1 - 2 * um, d0 + d1 - 2 * hl.v1, 0))
    # springer ends
    for k in (0, len(cu) - 1):
        (a0, b0), (c0, d0) = inner[k], outer[k]
        g.face((fr.P(a0, b0, c + proud), fr.P(c0, d0, c + proud), fr.P(c0, d0, c), fr.P(a0, b0, c)), f, (0, -1, 0))


def mould(g, T, fr, u0, u1, v, profile, f, ends=True, smooth=False):
    """A moulding along a wall from u0 to u1 at height v: profile [(out, up)] from the wall's face, bottom to top."""
    pts0 = [fr.P(u0, v + b, a) for a, b in profile]
    pts1 = [fr.P(u1, v + b, a) for a, b in profile]
    k = len(profile)
    for i in range(k - 1):
        da, db = profile[i + 1][0] - profile[i][0], profile[i + 1][1] - profile[i][1]
        nn = fr.D(0, -da, db)
        g.face((pts0[i], pts1[i], pts1[i + 1], pts0[i + 1]), f, nn)
    if ends:
        back0 = [fr.P(u0, v + profile[-1][1], 0), fr.P(u0, v + profile[0][1], 0)]
        back1 = [fr.P(u1, v + profile[-1][1], 0), fr.P(u1, v + profile[0][1], 0)]
        mu = (-fr.u[0], -fr.u[1], -fr.u[2])
        for i in range(k - 1):
            g.face((pts0[i], pts0[i + 1], fr.P(u0, v + profile[i + 1][1], 0), fr.P(u0, v + profile[i][1], 0)), f, mu)
            g.face((pts1[i], pts1[i + 1], fr.P(u1, v + profile[i + 1][1], 0), fr.P(u1, v + profile[i][1], 0)), f, fr.u)


CORNICE = [(0.0, 0.0), (.05, .0), (.05, .1), (.12, .16), (.12, .26), (.2, .34), (.2, .46), (.26, .5), (.26, .6), (0.0, .6)]
CORNICE_LOW = [(0.0, 0.0), (.06, 0.0), (.2, .34), (.26, .5), (.26, .6), (0.0, .6)]
STRING = [(0.0, 0.0), (.05, .03), (.05, .15), (0.0, .18)]
PLINTH = [(0.0, 0.0), (.07, 0.0), (.07, .78), (.03, .86), (0.0, .86)]
COPING = [(0.0, 0.0), (.06, 0.0), (.06, .1), (0.0, .16)]


def cornice(g, T, fr, u0, u1, v, f, scale=1.0, dentils=True):
    pr = CORNICE if T.i == 0 else CORNICE_LOW
    mould(g, T, fr, u0, u1, v, [(a * scale, b * scale) for a, b in pr], f)
    if dentils and T.detail:
        # corbel blocks under it
        step = .55 * scale if T.i == 0 else 1.1 * scale
        k = max(1, int((u1 - u0) / step))
        for i in range(k):
            x = u0 + (u1 - u0) * (i + .5) / k
            fbox(g, T, fr, x - .09 * scale, x + .09 * scale, v - .2 * scale, v + .02, 0, .1 * scale, f, .01)


# ---------------------------------------------------------------------------------------------------------------------------
# Small things
# ---------------------------------------------------------------------------------------------------------------------------
GLYPHS = {
    'A': '01110,10001,10001,11111,10001,10001,10001', 'B': '11110,10001,10001,11110,10001,10001,11110', 'C': '01111,10000,10000,10000,10000,10000,01111',
    'D': '11110,10001,10001,10001,10001,10001,11110', 'E': '11111,10000,10000,11110,10000,10000,11111', 'F': '11111,10000,10000,11110,10000,10000,10000',
    'G': '01111,10000,10000,10011,10001,10001,01111', 'H': '10001,10001,10001,11111,10001,10001,10001', 'I': '111,010,010,010,010,010,111',
    'K': '10001,10010,10100,11000,10100,10010,10001', 'L': '10000,10000,10000,10000,10000,10000,11111', 'M': '10001,11011,10101,10101,10001,10001,10001',
    'N': '10001,11001,10101,10011,10001,10001,10001', 'O': '01110,10001,10001,10001,10001,10001,01110', 'P': '11110,10001,10001,11110,10000,10000,10000',
    'R': '11110,10001,10001,11110,10100,10010,10001', 'S': '01111,10000,10000,01110,00001,00001,11110', 'T': '11111,00100,00100,00100,00100,00100,00100',
    'U': '10001,10001,10001,10001,10001,10001,01110', 'V': '10001,10001,10001,10001,01010,01010,00100', 'W': '10001,10001,10001,10101,10101,11011,10001',
    'Y': '10001,10001,01010,00100,00100,00100,00100', "'": '1,1,0,0,0,0,0', ' ': '000,000,000,000,000,000,000', '&': '01100,10010,10100,01000,10101,10010,01101',
    '.': '0,0,0,0,0,0,1', '-': '000,000,000,111,000,000,000', 'J': '00111,00010,00010,00010,00010,10010,01100', 'X': '10001,01010,00100,00100,00100,01010,10001',
    'Z': '11111,00001,00010,00100,01000,10000,11111', 'Q': '01110,10001,10001,10001,10101,10010,01101',
}


def text_width(s, px):
    return sum((len(GLYPHS.get(ch, GLYPHS[' ']).split(',')[0]) + 1) * px for ch in s) - px


def text(g, fr, u, v, px, s, f, c=.012, centre=True):
    """Lettering on a wall: 5 x 7 strokes of `px` metres a cell, at depth c, from (u, v) (its left foot, or its middle foot)."""
    x = u - (text_width(s, px) / 2 if centre else 0)
    # a wall is read from outside it, where its u runs right to left: the strokes are laid mirrored about u
    m = 2 * u
    for ch in s:
        rows = GLYPHS.get(ch, GLYPHS[' ']).split(',')
        for r, row in enumerate(rows):
            y = v + (6 - r) * px
            i = 0
            while i < len(row):
                if row[i] == '1':
                    j = i
                    while j < len(row) and row[j] == '1':
                        j += 1
                    g.face((fr.P(m - x - i * px, y, c), fr.P(m - x - j * px, y, c), fr.P(m - x - j * px, y + px, c), fr.P(m - x - i * px, y + px, c)), f, fr.n)
                    i = j
                else:
                    i += 1
        x += (len(rows[0]) + 1) * px


def chevrons(g, fr, u0, u1, v0, v1, c, f_stripe, pitch=.3):
    """Hazard chevrons (pointing along +u) across a band."""
    h = v1 - v0
    vm = (v0 + v1) / 2
    w = pitch / 2
    x = u0 - h / 2
    n = fr.n

    def clipq(pts):
        # clip a convex polygon to u0..u1
        out = pts
        for (lim, sgn) in ((u0, 1), (u1, -1)):
            inp, out = out, []
            for i in range(len(inp)):
                p, q = inp[i], inp[(i + 1) % len(inp)]
                sp, sq = (p[0] - lim) * sgn, (q[0] - lim) * sgn
                if sp >= 0:
                    out.append(p)
                if (sp >= 0) != (sq >= 0):
                    t = sp / (sp - sq)
                    out.append((p[0] + t * (q[0] - p[0]), p[1] + t * (q[1] - p[1])))
            if len(out) < 3:
                return []
        return out
    while x < u1:
        for (ya, yb, sa, sb) in ((v0, vm, 0.0, h / 2), (vm, v1, h / 2, 0.0)):
            poly = clipq([(x + sa, ya), (x + sa + w, ya), (x + sb + w, yb), (x + sb, yb)])
            if poly:
                g.face([fr.P(a, b, c) for a, b in poly], f_stripe, n)
        x += pitch


def band(g, T, hx, hz, y0, f_strap=None, lamp=True):
    """The load-bearing band round a member (in the member's own frame, y0 its base): a black steel strap from 1.0 to 1.6 m
    with yellow chevrons, bolted, a DANGER plate over it and the band lamp on each face (the lens glows as the runtime says)."""
    lo, hi = y0 + 1.0, y0 + 1.6
    for (nx, nz, half, reach) in ((0, 1, hx, hz), (0, -1, hx, hz), (1, 0, hz, hx), (-1, 0, hz, hx)):
        u = (nz, 0.0, -nx)
        fr = Frame((nx * reach - u[0] * half, 0.0, nz * reach - u[2] * half), u)
        w = 2 * half
        fbox(g, T, fr, -.012, w + .012, lo, hi, 0, .022, F['black'], .006)
        chevrons(g, fr, .0, w, lo + .07, hi - .07, .026, F['yellow'])
        if T.detail:
            for x in (.07, w - .07):
                for y in (lo + .035, hi - .035):
                    sk.cylinder(g, fr.P(x, y, .02), fr.P(x, y, .04), .018, F['steel'], 6)
        # the tag
        if w >= .9:
            fbox(g, T, fr, w / 2 - .3, w / 2 + .3, hi + .08, hi + .32, 0, .016, F['danger'], .006)
            if T.detail:
                text(g, fr, w / 2, hi + .13, .02, 'DANGER', F['white-paint'], .02)
        if lamp:
            x = w / 2
            ym = (lo + hi) / 2
            sk.cylinder(g, fr.P(x, ym, .02), fr.P(x, ym, .06), .075, F['iron'], T.round, caps=True)
            sk.lathe(g, [(.055, 0.0), (.05, .02), (.032, .04), (0.0, .05)], fr.P(x, ym, .06), F['lamp'], T.round, axis=fr.n)


def rivets(g, T, fr, pts, c, r=.016, f=None):
    if not T.detail:
        return
    f = F['steel'] if f is None else f
    for (a, b) in pts:
        sk.lathe(g, [(r, 0.0), (r * .8, r * .5), (0.0, r * .8)], fr.P(a, b, c), f, 5 if T.i else 6, axis=fr.n)


def pipe(g, T, fr, u, v0, v1, c=.09, r=.05, f=None):
    """A downpipe on a wall with its brackets and a hopper head."""
    f = F['iron'] if f is None else f
    sk.cylinder(g, fr.P(u, v0, c), fr.P(u, v1, c), r, f, T.small, caps=False)
    if T.detail:
        y = v0 + .6
        while y < v1 - .2:
            fbox(g, T, fr, u - r - .03, u + r + .03, y - .025, y + .025, 0, c + r * .4, f, .005)
            y += 2.4
        fbox(g, T, fr, u - .11, u + .11, v1 - .02, v1 + .2, .01, c + .1, f, .012)


def rng(seed):
    return random.Random(seed)


def brickbat(g, T, at, size, q, f):
    """A loose brick or lump: a chamfered box turned any way."""
    sk.obox(g, at, (size[0] / 2, size[1] / 2, size[2] / 2), q, f, None, 0)


def rand_quat(r):
    ax = (r.uniform(-1, 1), r.uniform(-1, 1), r.uniform(-1, 1))
    if abs(ax[0]) + abs(ax[1]) + abs(ax[2]) < .1:
        ax = (0, 1, 0)
    return sk.quat_axis(ax[0], ax[1], ax[2], r.uniform(0, math.pi * 2))


def yaw_quat(a):
    return sk.quat_axis(0, 1, 0, a)
