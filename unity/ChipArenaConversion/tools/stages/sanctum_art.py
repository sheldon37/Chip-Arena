"""The Hollow Sanctum's art (docs/arenas/SANCTUM.md, docs/arenas/STAGE-ART.md): the castle chapel modelled in Blender on the map's
own blocks and its Core kit's parts, into the stage bank Resources/ChipArenaStageSanctum (stagekit.py's format and bake). Runtime
NativeStageArt draws it in place of the boxes; without the bank the box kit draws as before.

usage (from unity/ChipArenaConversion):
  dotnet run --project Tests/CoreTests.csproj -c Release -- --stage-layout sanctum LAYOUT.json
  blender --background --factory-startup --python tools/stages/sanctum_art.py -- --layout LAYOUT.json --out ASSETS/LocalAssets
          [--preview DIR] [--tiers 0,1,2] [--no-ao] [--static-only] [--own-mid]

The bank's budget is 20 MB (docs/arenas/STAGE-ART.md): the static scenery is built on the Sanctum's own tiers (`Cut`), its Fine
tier cut to what reads at play distance; its Mid props share the Fine meshes (nothing stored twice; `--own-mid` builds a Mid
tier of its own, about 5 MB more); the Low tier is the Low graphics tier's. The rigs and the pews keep stagekit's tiers.

The architecture (this file): the walls as one face per wall plane with the lancets and roses cut through it, splayed reveals and
real tracery at the kit's own tracery plane (so the stained glass, Core NativeSanctumScenery, sits behind it as before); the
clustered piers with moulded bases and capitals; ribbed groin vaults (webs, swept ribs, gilded bosses) springing where the kit's
ribs sprang; the oculus ring; flagstone floors, the runner, the brass rings and the crossing's star; galleries with cornices and
oak-beamed soffits; balustrades of blind quatrefoils; stairs with rounded nosings; the blind arcading; wax candles with drips
wherever the kit stands one; the crypt's columns and low vault. The furniture, the rigs and the breakable pews: sanctum_props.py.
Everything else the kit carries is rebuilt from its own parts as chamfered, finished pieces (round where the kit's part is a bar).
"""
import argparse
import math
import os
import sys
import time

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stagekit as sk  # noqa: E402
from stagekit import Geo, box, obox, sweep, lathe, grid_face, pointed_arch, arch_width  # noqa: E402

BANK = 'ChipArenaStageSanctum'
F = {}            # finishes by name
KIND = []         # kit kind index -> finish


def finishes(L):
    kc = dict(zip(L['kinds'], L['kindColors']))

    def K(k):
        return kc['sanctum-kit-' + k]
    A = sk
    F['wall'] = A.fin('wall', 0x7C746A, A.DETAIL_ASHLAR, smooth=.16, moss=.25, param=.9)
    F['pier'] = A.fin('pier', 0x9E9588, A.DETAIL_ASHLAR, smooth=.2, moss=.12, param=.72)
    F['shaft'] = A.fin('shaft', 0xA89F92, A.DETAIL_CARVED, smooth=.26, wear=.25)
    F['trim'] = A.fin('trim', K('stone-trim'), A.DETAIL_CARVED, smooth=.22, wear=.3)
    F['stone'] = A.fin('stone', K('stone'), A.DETAIL_CARVED, smooth=.2, wear=.25)
    F['dark'] = A.fin('dark', K('stone-dark'), A.DETAIL_CARVED, smooth=.14, dirt=.7)
    F['niche'] = A.fin('niche', 0x2E2A34, A.DETAIL_PLASTER, smooth=.08, dirt=.5)
    F['rib'] = A.fin('rib', 0xC2B8A6, A.DETAIL_CARVED, smooth=.24, wear=.3)
    F['web'] = A.fin('web', 0x625A6C, A.DETAIL_PLASTER, smooth=.08, dirt=.5)
    F['floor'] = A.fin('floor', 0x675E56, A.DETAIL_FLAG, smooth=.34, moss=.08)
    F['gfloor'] = A.fin('gfloor', 0x706660, A.DETAIL_FLAG, smooth=.3)
    F['soffit'] = A.fin('soffit', 0x5E5862, A.DETAIL_PLASTER, smooth=.08)
    F['cryptfloor'] = A.fin('cryptfloor', 0x46424C, A.DETAIL_FLAG, smooth=.32, moss=.55, param=.55)
    F['cryptwall'] = A.fin('cryptwall', 0x55505C, A.DETAIL_ASHLAR, smooth=.2, moss=.7, param=.62)
    F['marble'] = A.fin('marble', K('marble'), A.DETAIL_MARBLE, smooth=.72, dirt=.4)
    F['blackmarble'] = A.fin('blackmarble', 0x2A262E, A.DETAIL_MARBLE, smooth=.78, dirt=.3)
    F['gold'] = A.fin('gold', K('gold'), A.DETAIL_BRASS, smooth=.8, metal=1.0, dirt=.25, wear=.2)
    F['brass'] = A.fin('brass', K('brass'), A.DETAIL_BRASS, smooth=.72, metal=1.0, dirt=.35, wear=.25)
    F['iron'] = A.fin('iron', 0x2C2A32, A.DETAIL_IRON, smooth=.34, metal=.75, wear=.35)
    F['bronze'] = A.fin('bronze', K('bronze'), A.DETAIL_BRASS, smooth=.55, metal=1.0, dirt=.5)
    F['wax'] = A.fin('wax', K('wax'), A.DETAIL_WAX, smooth=.5, dirt=.15)
    F['wick'] = A.fin('wick', 0x1A1410, A.DETAIL_NONE, smooth=.1, dirt=0)
    for axis, tag in ((0, 'x'), (1, 'y'), (2, 'z')):
        F['oak' + tag] = A.fin('oak' + tag, K('oak'), A.DETAIL_WOOD, smooth=.42, param=axis, wear=.3)
        F['oakdark' + tag] = A.fin('oakdark' + tag, K('oak-dark'), A.DETAIL_WOOD, smooth=.38, param=axis, wear=.25)
    F['red'] = A.fin('red', K('cloth-red'), A.DETAIL_CLOTH, smooth=.12, dirt=.5)
    F['blue'] = A.fin('blue', K('cloth-blue'), A.DETAIL_CLOTH, smooth=.12, dirt=.5)
    F['clothgold'] = A.fin('clothgold', K('cloth-gold'), A.DETAIL_CLOTH, smooth=.35, metal=.4, dirt=.3)
    F['velvet'] = A.fin('velvet', K('velvet'), A.DETAIL_CLOTH, smooth=.18, dirt=.4)
    F['carpet'] = A.fin('carpet', K('carpet'), A.DETAIL_CLOTH, smooth=.1, dirt=.6)
    F['pipe'] = A.fin('pipe', K('pipe'), A.DETAIL_BRASS, smooth=.7, metal=1.0, dirt=.3)
    F['lead'] = A.fin('lead', K('lead'), A.DETAIL_IRON, smooth=.3, metal=.6)
    F['statue'] = A.fin('statue', K('statue'), A.DETAIL_CARVED, smooth=.28, wear=.2, dirt=.8)
    F['bone'] = A.fin('bone', K('bone'), A.DETAIL_CARVED, smooth=.3)
    F['silver'] = A.fin('silver', K('silver'), A.DETAIL_BRASS, smooth=.82, metal=1.0, dirt=.3)
    F['chain'] = A.fin('chain', K('chain'), A.DETAIL_IRON, smooth=.4, metal=.85)
    F['rope'] = A.fin('rope', K('rope'), A.DETAIL_CLOTH, smooth=.1)
    F['coal'] = A.fin('coal', K('coal'), A.DETAIL_IRON, smooth=.15, metal=.2)
    F['inlay'] = A.fin('inlay', K('inlay'), A.DETAIL_BRASS, smooth=.6, metal=.8)
    F['glass'] = A.fin('glass', K('glass'), A.DETAIL_NONE, smooth=.9)
    F['glow'] = A.fin('glow', K('glow'), A.DETAIL_GLOW, glow=2.6)
    F['ember'] = A.fin('ember', K('ember'), A.DETAIL_GLOW, glow=3.0)
    F['lampred'] = A.fin('lampred', 0xFF5A30, A.DETAIL_GLOW, glow=2.2)
    order = ['stone', 'dark', 'trim', 'marble', 'gold', 'iron', 'bronze', 'wax', 'oakz', 'oakdarkz', 'red', 'blue', 'clothgold', 'pipe', 'lead',
             'coal', 'inlay', 'rope', 'chain', 'velvet', 'statue', 'brass', 'glow', 'glass', 'carpet', 'bone', 'silver', 'ember']
    KIND.extend(F[k] for k in order)


class Cut(sk.Tier):
    """The Sanctum's measure of each tier: stagekit's, leaner where the detail does not read at play distance (the bank's budget
    is 20 MB: docs/arenas/STAGE-ART.md, "The bank's budget"). A curve takes the segments its turn needs: a pointed arch's half
    turns sixty degrees or so, so it takes `arch`, not the half circle's `arc`; flat faces are cut no finer than the bake's
    mottling; what is only ever seen from the floor (the vault, the capitals) is built for that distance."""

    def __init__(self, i):
        super().__init__(i)
        self.arc = (16, 10, 6)[i]          # segments of a half circle
        self.arch = (8, 5, 3)[i]           # segments of a pointed arch's half
        self.round = (10, 7, 5)[i]         # sides of a round section
        self.small = (6, 4, 3)[i]          # sides of a small round
        self.cell = (1.0, 3.0, 8.0)[i]     # the subdivision of a flat face


def kind_fin(k, axis=None):
    f = KIND[k]
    if axis is not None and sk.FINS[f].detail == sk.DETAIL_WOOD:
        base = 'oakdark' if 'dark' in sk.FINS[f].name else 'oak'
        return F[base + 'xyz'[axis]]
    return f


# ---------------------------------------------------------------------------------------------------------------------------
# Wall planes
# ---------------------------------------------------------------------------------------------------------------------------
Y = (0.0, 1.0, 0.0)


class Plane:
    """A wall's inner face: point(u, y, d) = origin + u U + y Y - d N (d into the wall), N the face's normal into the room and
    U = N x Y, so mouldings swept along U face the room."""

    def __init__(self, name, origin, N, u0, u1, y0=0.0, y1=21.0, thick=.8):
        self.name, self.o, self.N = name, origin, N
        self.U = sk._cross(N, Y)
        self.u0, self.u1, self.y0, self.y1, self.thick = u0, u1, y0, y1, thick
        self.holes = []

    def at(self, u, y, d=0.0):
        o, U, N = self.o, self.U, self.N
        return (o[0] + U[0] * u - N[0] * d, y, o[2] + U[2] * u - N[2] * d)

    def u_of(self, x, z):
        return (x - self.o[0]) * self.U[0] + (z - self.o[2]) * self.U[2]


class Lancet:
    """A two-light lancet: the inner face's opening (the block's: half 1.2 from the sill to the apex) splaying to the jambs at the
    tracery plane (half L from the kit: the arch springs 1.6 under the head)."""

    def __init__(self, u, w):
        self.u, self.w = u, w
        self.hw = w['half']
        self.y0 = w['c'][1] - w['halfY']
        self.y1 = w['c'][1] + w['halfY']
        self.L = self.hw - .14
        self.spring = self.y1 - 1.6
        self.sill = self.y0 + .16
        self.in_spring = self.y1 - 1.75

    def band(self):
        return self.y0, self.y1

    def column(self):
        return self.u - self.hw, self.u + self.hw

    def half_in(self, y):
        return arch_width(self.hw, self.y1 - self.in_spring, y - self.in_spring)

    def half_out(self, y):
        return arch_width(self.L, self.y1 - self.spring, y - self.spring)

    def rows(self, T):
        # the head's rows at even turns of its arch (not even heights: the apex would be cut blunt)
        return [self.y0, self.in_spring] + [self.in_spring + b for (a, b) in pointed_arch(self.hw, self.y1 - self.in_spring, T.arch)[1:]]


class Rose:
    def __init__(self, u, w):
        self.u, self.w = u, w
        self.R = w['half']
        self.yc = w['c'][1]
        self.r = self.R - .18

    def band(self):
        return self.yc - self.R, self.yc + self.R

    def column(self):
        return self.u - self.R, self.u + self.R

    def half_in(self, y):
        d = y - self.yc
        return math.sqrt(max(0.0, self.R * self.R - d * d))

    def rows(self, T):
        n = T.arc * 2
        return [self.yc - self.R * math.cos(math.pi * i / n) for i in range(n + 1)]


def wall_face(g, pl, T, f):
    """The plane's inner face round its holes (a grid away from them, strips about each hole) and each hole's reveal."""
    us = {pl.u0, pl.u1}
    ys = {pl.y0, pl.y1}
    for h in pl.holes:
        a, b = h.column()
        us.update((a, b))
        ya, yb = h.band()
        ys.update((ya, yb))
    us, ys = sorted(us), sorted(ys)
    for i in range(len(us) - 1):
        for j in range(len(ys) - 1):
            ua, ub, ya, yb = us[i], us[i + 1], ys[j], ys[j + 1]
            if ub - ua < 1e-4 or yb - ya < 1e-4:
                continue
            uc, yc = (ua + ub) / 2, (ya + yb) / 2
            inside = False
            for h in pl.holes:
                a, b = h.column()
                c, d = h.band()
                if a <= uc <= b and c <= yc <= d:
                    inside = True
            if inside:
                continue
            grid_face(g, pl.at(ua, ya), sk._sub(pl.at(ub, ya), pl.at(ua, ya)), (0, yb - ya, 0), 0, 0, f, pl.N, T.cell)
    for h in pl.holes:
        a, b = h.column()
        rows = h.rows(T)
        for k in range(len(rows) - 1):
            y0, y1 = rows[k], rows[k + 1]
            w0, w1 = h.half_in(y0), h.half_in(y1)
            if (h.u - a) - w0 > 1e-3 or (h.u - a) - w1 > 1e-3:
                g.face((pl.at(a, y0), pl.at(h.u - w0, y0), pl.at(h.u - w1, y1), pl.at(a, y1)), f, pl.N)
                g.face((pl.at(h.u + w0, y0), pl.at(b, y0), pl.at(b, y1), pl.at(h.u + w1, y1)), f, pl.N)
        reveal(g, pl, h, T, f)


def reveal(g, pl, h, T, f):
    """The opening through the wall: the inner outline to the jambs' at the tracery plane, then straight through."""
    dt, dw = .62, pl.thick
    if isinstance(h, Lancet):
        rows = h.rows(T)
        ss = [(y - h.y0) / (h.y1 - h.y0) for y in rows]

        def pin(s, side):
            y = h.y0 + (h.y1 - h.y0) * s
            return pl.at(h.u + side * h.half_in(y), y, 0.0)

        def pout(s, side, d):
            y = h.sill + (h.y1 - h.sill) * s
            return pl.at(h.u + side * h.half_out(y), y, d)
        for side in (-1, 1):
            # the outline's normal into the opening at each row (from its neighbours; the two sides meet in a crease at the apex)
            q = [(side * h.half_in(y), y) for y in rows]
            nin = []
            for k in range(len(q)):
                p, n = q[max(0, k - 1)], q[min(len(q) - 1, k + 1)]
                nu, ny = n[1] - p[1], -(n[0] - p[0])
                if nu * side > 0:
                    nu, ny = -nu, -ny
                nin.append(sk._norm((pl.U[0] * nu, ny, pl.U[2] * nu)))
            for k in range(len(ss) - 1):
                a0, a1 = pin(ss[k], side), pin(ss[k + 1], side)
                b0, b1 = pout(ss[k], side, dt), pout(ss[k + 1], side, dt)
                c0, c1 = pout(ss[k], side, dw), pout(ss[k + 1], side, dw)
                # the splay's normal points into the opening and toward the room
                s0, s1 = sk._norm(sk._add(nin[k], pl.N, .6)), sk._norm(sk._add(nin[k + 1], pl.N, .6))
                g.face((a0, a1, b1, b0), f, normals=(s0, s1, s1, s0))
                g.face((b0, b1, c1, c0), f, normals=(nin[k], nin[k + 1], nin[k + 1], nin[k]))
        # the sill slopes down into the hall
        cells = max(1, int(2 * h.hw / T.cell))
        for i in range(cells):
            s0, s1 = -1 + 2 * i / cells, -1 + 2 * (i + 1) / cells
            g.face((pl.at(h.u + s0 * h.hw, h.y0, 0), pl.at(h.u + s1 * h.hw, h.y0, 0), pl.at(h.u + s1 * h.L, h.sill, dt), pl.at(h.u + s0 * h.L, h.sill, dt)), f, sk._norm(sk._add(Y, pl.N, .35)))
            g.face((pl.at(h.u + s0 * h.L, h.sill, dt), pl.at(h.u + s1 * h.L, h.sill, dt), pl.at(h.u + s1 * h.L, h.sill, dw), pl.at(h.u + s0 * h.L, h.sill, dw)), f, Y)
    else:
        n = T.arc * 4
        for k in range(n):
            a0, a1 = 2 * math.pi * k / n, 2 * math.pi * (k + 1) / n
            pi0 = pl.at(h.u + h.R * math.cos(a0), h.yc + h.R * math.sin(a0), 0)
            pi1 = pl.at(h.u + h.R * math.cos(a1), h.yc + h.R * math.sin(a1), 0)
            po0 = pl.at(h.u + h.r * math.cos(a0), h.yc + h.r * math.sin(a0), dt)
            po1 = pl.at(h.u + h.r * math.cos(a1), h.yc + h.r * math.sin(a1), dt)
            pw0 = pl.at(h.u + h.r * math.cos(a0), h.yc + h.r * math.sin(a0), dw)
            pw1 = pl.at(h.u + h.r * math.cos(a1), h.yc + h.r * math.sin(a1), dw)
            in0 = (-pl.U[0] * math.cos(a0), -math.sin(a0), -pl.U[2] * math.cos(a0))
            in1 = (-pl.U[0] * math.cos(a1), -math.sin(a1), -pl.U[2] * math.cos(a1))
            s0, s1 = sk._norm(sk._add(in0, pl.N, .5)), sk._norm(sk._add(in1, pl.N, .5))
            g.face((pi0, pi1, po1, po0), f, normals=(s0, s1, s1, s0))
            g.face((po0, po1, pw1, pw0), f, normals=(in0, in1, in1, in0))


def tracery(g, pl, h, T):
    """The window's stone tracery at the kit's tracery plane (.18 m outward of the wall's middle), in front of the glass."""
    d = pl.thick / 2 + .18 - .06
    st = F['trim']
    bar = [(-.05, -.08), (.05, -.08), (.06, 0), (.05, .08), (-.05, .08), (-.06, 0)] if T.fine else sk.rect_section(.1, .16)

    def path_u(points):
        return [pl.at(h.u + u, y, d) for (u, y) in points]
    nrm = [pl.N]
    if isinstance(h, Lancet):
        # a roll moulding round the jambs and the arch
        pts = [(-h.L, h.sill), (-h.L, h.spring)] + [(-h.L + a, h.spring + b) for (a, b) in pointed_arch(h.L, h.y1 - h.spring, T.arch)[1:]]
        right = [(-u, y) for (u, y) in reversed(pts[:-1])]
        outline = pts + right
        sweep(g, sk.circle_section(.055, (6, 4, 3)[T.i]), path_u(outline), st, path_normals=[pl.N] * len(outline), caps=False)
        # the mullion
        sweep(g, bar, path_u([(0, h.sill), (0, h.spring + .55)]), st, path_normals=[pl.N] * 2)
        # the lights' heads: a pointed trefoil head each side
        for s in (-1, 1):
            cu, l2 = s * (h.L / 2 + .02), h.L / 2 - .05
            head = []
            hn = (6, 4, 2)[T.i]
            for (a, b) in pointed_arch(l2, .62, hn):
                head.append((cu - l2 + a, h.spring + b))
            head += [(cu + l2 - a, h.spring + b) for (a, b) in reversed(pointed_arch(l2, .62, hn)[:-1])]
            sweep(g, bar, path_u(head), st, path_normals=[pl.N] * len(head))
            if T.tiny:
                # two cusps in each head
                for c in (-1, 1):
                    cx, cy = cu + c * l2 * .55, h.spring + .3
                    cusp = [(cx + c * .0, cy - .12), (cx - c * .1, cy), (cx, cy + .12)]
                    sweep(g, sk.rect_section(.05, .1), path_u(cusp), st, path_normals=[pl.N] * 3, caps=False)
        # the eye: a ring and a quatrefoil
        ey, er = h.spring + 1.0, .3
        ring = [(er * math.cos(2 * math.pi * i / T.arc), ey + er * math.sin(2 * math.pi * i / T.arc)) for i in range(T.arc)]
        sweep(g, bar, path_u(ring), st, closed=True, path_normals=[pl.N] * len(ring))
        if T.detail:
            n = 24 if T.fine else 12
            q = []
            for i in range(n):
                a = 2 * math.pi * i / n
                r = .12 + .12 * abs(math.cos(2 * a)) ** .7
                q.append((r * math.cos(a + math.pi / 4), ey + r * math.sin(a + math.pi / 4)))
            sweep(g, sk.rect_section(.05, .12), path_u(q), st, closed=True, path_normals=[pl.N] * len(q))
    else:
        r = h.r
        spokes = 12 if h.w['design'] % 2 == 0 else 16

        def ring(rr, sec):
            n = max(16, int(T.arc * 2 * rr / 2))
            pts = [(rr * math.cos(2 * math.pi * i / n), h.yc + rr * math.sin(2 * math.pi * i / n)) for i in range(n)]
            sweep(g, sec, path_u(pts), st, closed=True, path_normals=[pl.N] * n)
        ring(r - .06, sk.rect_section(.16, .2, .03 if T.fine else 0))
        ring(r * .62, sk.rect_section(.12, .18))
        ring(r * .26, sk.rect_section(.12, .18))
        for i in range(spokes):
            a = 2 * math.pi * (i + .5) / spokes
            p0 = (r * .26 * math.cos(a), h.yc + r * .26 * math.sin(a))
            p1 = ((r - .1) * math.cos(a), h.yc + (r - .1) * math.sin(a))
            sweep(g, sk.rect_section(.1, .18), path_u([p0, p1]), st, path_normals=[pl.N] * 2, caps=False)
        if T.detail:
            for i in range(spokes):
                a = 2 * math.pi * i / spokes
                rr, rp = r * .81, r * .14
                n = 10 if T.fine else 6
                pts = [(rr * math.cos(a) + rp * math.cos(2 * math.pi * j / n), h.yc + rr * math.sin(a) + rp * math.sin(2 * math.pi * j / n)) for j in range(n)]
                sweep(g, sk.rect_section(.06, .12), path_u(pts), st, closed=True, path_normals=[pl.N] * n)
            for i in range(spokes if T.tiny else 0):
                a = 2 * math.pi * (i + .5) / spokes
                rr, rp = r * .44, r * .09
                n = 8
                pts = [(rr * math.cos(a) + rp * math.cos(2 * math.pi * j / n), h.yc + rr * math.sin(a) + rp * math.sin(2 * math.pi * j / n)) for j in range(n)]
                sweep(g, sk.rect_section(.05, .1), path_u(pts), st, closed=True, path_normals=[pl.N] * n)
        # the gilded boss in the eye
        c = pl.at(h.u, h.yc, d - .06)
        lathe(g, [(0, -.04), (.2, 0), (.24, .06), (.16, .14), (0, .2)], c, F['gold'], T.round, axis=pl.N, cap0=True)


def moulding(g, pl, profile, y, f, u0=None, u1=None, T=None):
    """A moulding along the plane at height y: profile [(out, up)] (out from the face into the hall)."""
    a = pl.u0 if u0 is None else u0
    b = pl.u1 if u1 is None else u1
    path = [pl.at(a, y), pl.at(b, y)]
    sweep(g, profile, path, f, path_normals=[Y, Y], smooth_u=False)


PLINTH = [(-.01, 0.0), (.15, 0.0), (.15, .24), (.12, .28), (.1, .34), (.07, .42), (.04, .47), (-.01, .5)]
STRING = [(-.01, -.3), (.04, -.3), (.08, -.24), (.17, -.1), (.2, -.02), (.18, .02), (.08, .02), (-.01, .0)]
CORNICE = [(-.02, -.06), (.04, -.06), (.1, .0), (.14, .08), (.12, .14), (.08, .18), (-.02, .2)]


def blind_arcade(g, pl, u0, u1, y0, y1, T):
    """Blind arcading on the face: shafts, pointed arches over dark niches, bands above and below (the kit's runs)."""
    n = int(math.floor((u1 - u0 - .3) / 1.5))
    if n < 1:
        return
    pitch = (u1 - u0) / n
    head = y1 - .55
    st, nf = F['trim'], F['niche']
    for i in range(n):
        c = u0 + pitch * (i + .5)
        w = pitch * .36
        # the niche's back, a hair proud of the face
        outline = [(c - w, y0), (c + w, y0)]
        arc = pointed_arch(w, .5, (5, 3, 2)[T.i])
        outline += [(c + w - a, head + b) for (a, b) in arc[1:]]
        outline += [(c - w + a, head + b) for (a, b) in reversed(arc[:-1])]
        pts = [pl.at(u, y, -.006) for (u, y) in outline]
        # fan from the niche's middle (convex)
        mid = pl.at(c, (y0 + head) / 2, -.006)
        for k in range(len(pts)):
            g.face((mid, pts[k], pts[(k + 1) % len(pts)]), nf, pl.N)
        # the arch moulding over it
        ap = [(c - w - .04, head)] + [(c - w - .04 + a * (w + .04) / w, head + b * 1.06) for (a, b) in arc[1:]]
        ap += [(c + w + .04 - a * (w + .04) / w, head + b * 1.06) for (a, b) in reversed(arc[:-1])]
        if T.detail:
            sweep(g, [(-.01, -.04), (.05, -.04), (.07, 0), (.05, .04), (-.01, .04)] if T.fine else [(-.01, -.04), (.07, 0), (-.01, .04)],
                  [pl.at(u, y, 0) for (u, y) in ap], st, path_normals=[pl.N] * len(ap), up=None, caps=T.fine)
        # the shafts between the niches
        if i > 0 and T.detail:
            cu = u0 + pitch * i
            base = pl.at(cu, y0, -.07)
            top = pl.at(cu, head + .05, -.07)
            sk.cylinder(g, base, top, .05, F['shaft'], T.small if T.fine else 3, caps=False)
            if T.tiny:
                lathe(g, [(.07, 0), (.075, .05), (.055, .1)], base, st, T.small)
                lathe(g, [(.055, -.1), (.085, -.01), (.09, .06)], top, st, T.small, cap1=True)
    moulding(g, pl, [(-.01, 0), (.1, 0), (.1, .1), (.06, .14), (-.01, .14)], y1, st, u0, u1)
    moulding(g, pl, [(-.01, -.1), (.08, -.1), (.08, 0), (-.01, 0)], y0, st, u0, u1)


def wall_shaft(g, pl, u, y0, y1, T):
    """A wall shaft at a bay line rising to the vault's springing, with its base and capital."""
    p0, p1 = pl.at(u, y0, -.11), pl.at(u, y1, -.11)
    sk.cylinder(g, p0, p1, .11, F['shaft'], T.round, caps=False)
    lathe(g, [(.14, 0), (.14, .12), (.125, .16), (.12, .22), (.11, .26)] if T.fine else [(.14, 0), (.14, .12), (.11, .26)], p0, F['trim'], T.round)
    capital(g, p1, .12, T)


def capital(g, top, r, T, f=None):
    """A bell capital under an abacus at `top` (the springing)."""
    f = F['trim'] if f is None else f
    # (the springing is ten metres over the floor and six over the galleries: the necking's fillet and the leaves' roundness
    # do not read from there)
    bell = [(r, -.62), (r + .02, -.58), (r * 1.08, -.4), (r * 1.4, -.18), (r * 1.62, -.06), (r * 1.62, 0.0)]
    if not T.fine:
        bell = [bell[0], bell[3], bell[5]]
    lathe(g, bell, top, f, (10, 6, 4)[T.i], cap1=False)
    lathe(g, [(r * 1.75, 0.0), (r * 1.75, .1), (r * 1.6, .16)] if T.fine else [(r * 1.75, 0.0), (r * 1.7, .15)], top, f, (8, 6, 4)[T.i], cap1=True, phase=math.pi / 8)
    if T.tiny:
        # crockets: leaves curling out under the abacus
        for i in range(6):
            a = 2 * math.pi * i / 6 + .3
            c = (math.cos(a), 0, math.sin(a))
            base = sk._add(top, c, r * 1.05)
            base = (base[0], top[1] - .42, base[2])
            tip = sk._add(top, c, r * 1.7)
            tip = (tip[0], top[1] - .06, tip[2])
            mid = sk._add(sk._lerp(base, tip, .55), c, .06)
            sk.tube(g, [base, mid, tip], .035, f, 3, caps=False)


# ---------------------------------------------------------------------------------------------------------------------------
# The walls
# ---------------------------------------------------------------------------------------------------------------------------
def walls(g, L, T):
    W = L['windows']
    planes = []
    for s in (-1, 1):
        for half in (-1, 1):
            u0, u1 = (-30.0, -6.0) if half < 0 else (6.0, 30.0)
            N = (0, 0, -s)
            pl = Plane('aisle', (0.0, 0.0, s * 11.5), N, 0, 0)
            a, b = sorted((pl.u_of(u0, 0), pl.u_of(u1, 0)))
            pl.u0, pl.u1 = a, b
            planes.append(pl)
    for sx in (-1, 1):
        for sz in (-1, 1):
            N = (-sx, 0, 0)
            pl = Plane('arm', (sx * 6.0, 0.0, 0.0), N, 0, 0)
            a, b = sorted((pl.u_of(0, sz * 11.5), pl.u_of(0, sz * 20.0)))
            pl.u0, pl.u1 = a, b
            planes.append(pl)
    for sz in (-1, 1):
        pl = Plane('armend', (0.0, 0.0, sz * 20.0), (0, 0, -sz), 0, 0)
        a, b = sorted((pl.u_of(-6, 0), pl.u_of(6, 0)))
        pl.u0, pl.u1 = a, b
        planes.append(pl)
    for sx in (-1, 1):
        pl = Plane('end', (sx * 30.0, 0.0, 0.0), (-sx, 0, 0), 0, 0)
        a, b = sorted((pl.u_of(0, -11.5), pl.u_of(0, 11.5)))
        pl.u0, pl.u1 = a, b
        planes.append(pl)
    # each window into its plane: the one whose face lies .4 m in front of the window's centre
    for w in W:
        x, y, z = w['c']
        for pl in planes:
            fx, fz = x + w['nx'] * .4, z + w['nz'] * .4
            onplane = abs((fx - pl.o[0]) * pl.N[0] + (fz - pl.o[2]) * pl.N[2]) < .05 and abs(pl.N[0] - w['nx']) < .01 and abs(pl.N[2] - w['nz']) < .01
            if onplane:
                u = pl.u_of(x, z)
                if pl.u0 - .01 <= u <= pl.u1 + .01:
                    pl.holes.append(Rose(u, w) if w['rose'] else Lancet(u, w))
    for pl in planes:
        wall_face(g, pl, T, F['wall'])
        for h in pl.holes:
            tracery(g, pl, h, T)
        moulding(g, pl, PLINTH, 0.0, F['trim'])
        if pl.name in ('aisle', 'arm'):
            moulding(g, pl, STRING, 8.0, F['trim'])
        if pl.name == 'aisle':
            blind_arcade(g, pl, pl.u0, pl.u1, .6, 2.9, T)
            blind_arcade(g, pl, pl.u0, pl.u1, 4.4, 7.3, T)
            for x in (-24.0, -18.0, -12.0, 12.0, 18.0, 24.0):
                u = pl.u_of(x, 0)
                if pl.u0 + .3 < u < pl.u1 - .3:
                    wall_shaft(g, pl, u, .5, 13.0, T)
        if pl.name == 'arm':
            blind_arcade(g, pl, pl.u0, pl.u1, .6, 3.6, T)
        # a shaft in each of the plane's corners where it meets its neighbour, rising to the springing
        for u in (pl.u0, pl.u1):
            p0 = pl.at(u, .5, -.1)
            sk.cylinder(g, p0, pl.at(u, 13.0, -.1), .11, F['shaft'], T.round, caps=False)
    return planes


# ---------------------------------------------------------------------------------------------------------------------------
# Piers and columns
# ---------------------------------------------------------------------------------------------------------------------------
def octagon(cx, cz, h, c, y):
    return [(cx + h, y, cz - h + c), (cx + h, y, cz + h - c), (cx + h - c, y, cz + h), (cx - h + c, y, cz + h),
            (cx - h, y, cz + h - c), (cx - h, y, cz - h + c), (cx - h + c, y, cz - h), (cx + h - c, y, cz - h)]


def prism(g, poly_bottom, height, f, T, cap=True):
    """A vertical prism over a convex plan polygon (counter-clockwise from above or not: faces orient themselves)."""
    n = len(poly_bottom)
    cx = sum(p[0] for p in poly_bottom) / n
    cz = sum(p[2] for p in poly_bottom) / n
    for i in range(n):
        a, b = poly_bottom[i], poly_bottom[(i + 1) % n]
        out = sk._norm((((a[0] + b[0]) / 2) - cx, 0, ((a[2] + b[2]) / 2) - cz))
        rows = max(1, int(height / max(T.cell, 1.5)))
        for r in range(rows):
            y0, y1 = a[1] + height * r / rows, a[1] + height * (r + 1) / rows
            g.face(((a[0], y0, a[2]), (b[0], y0, b[2]), (b[0], y1, b[2]), (a[0], y1, a[2])), f, out)
    if cap:
        g.face([(p[0], p[1] + height, p[2]) for p in poly_bottom], f, Y)


def sweep_sides(g, section, path, f, keep, **kw):
    """sweep() with flat sides, only the section's sides in `keep` (side j runs from section[j] to section[j + 1]): a moulding
    without its back against the wall and its foot on the floor, which nothing ever sees."""
    t = Geo()
    sweep(t, section, path, f, caps=False, smooth_u=False, **kw)
    k = len(section)
    if t.faces % k or any(c != 4 for c in t.size):
        g.extend(t)
        return
    for i in range(t.faces):
        if i % k in keep:
            g.p.extend(t.p[i * 12:i * 12 + 12])
            g.n.extend(t.n[i * 12:i * 12 + 12])
            g.size.append(4)
            g.fin.append(f)


def mould_round(g, poly, profile, f, T, keep=None):
    """A moulding swept round a closed plan polygon: profile [(out, up)]; keep: the profile's sides drawn (None: all)."""
    path = [(p[0], p[1], p[2]) for p in poly]
    n = len(path)
    cx = sum(p[0] for p in path) / n
    cz = sum(p[2] for p in path) / n
    # sweep's section x is the frame's u = v x T with v = up; for a counter-clockwise path seen from above that points out
    area = 0.0
    for i in range(n):
        a, b = path[i], path[(i + 1) % n]
        area += a[0] * b[2] - b[0] * a[2]
    if area < 0:
        path = path[::-1]
    if keep is None:
        sweep(g, profile, path, f, closed=True, path_normals=[Y] * n, smooth_u=False)
    else:
        sweep_sides(g, profile, path, f, keep, closed=True, path_normals=[Y] * n)


def pier(g, b, T, crossing):
    x0, x1 = b['pos'][0] - b['size'][0] / 2, b['pos'][0] + b['size'][0] / 2
    z0, z1 = b['pos'][2] - b['size'][2] / 2, b['pos'][2] + b['size'][2] / 2
    cx, cz, h = (x0 + x1) / 2, (z0 + z1) / 2, (x1 - x0) / 2
    spring = 12.0 if crossing else 13.0
    c = h * .42
    # the plinth (an octagon), its mouldings, the core, the shafts, the capital band
    prism(g, octagon(cx, cz, h + .14, (h + .14) * .42, 0.0), .34, F['trim'], T)
    # (the mouldings without their feet and backs; simpler on the Mid tier, sixteen metres off; none on the Low one)
    if T.fine:
        mould_round(g, octagon(cx, cz, h + .06, (h + .06) * .42, .34), [(-.02, 0), (.06, 0), (.08, .04), (.06, .08), (.02, .1), (.04, .16), (.02, .2), (-.02, .22)], F['trim'], T, keep=(1, 2, 3, 4, 5, 6))
    elif T.i == 1:
        mould_round(g, octagon(cx, cz, h + .06, (h + .06) * .42, .34), [(.07, 0), (.06, .09), (-.02, .22)], F['trim'], T, keep=(0, 1))
    prism(g, octagon(cx, cz, h, c, .34 if T.i == 2 else .5), spring - (.34 if T.i == 2 else .5), F['pier'], T, cap=False)
    r = .13
    shafts = [(cx - h - r + .06, cz), (cx + h + r - .06, cz), (cx, cz - h - r + .06), (cx, cz + h + r - .06)]
    if crossing:
        d = h - c / 2 + .06
        shafts += [(cx + sx * d, cz + sz * d) for sx in (-1, 1) for sz in (-1, 1)]
    elif T.i < 2:
        d = h - c / 2 + .02
        for sx in (-1, 1):
            for sz in (-1, 1):
                p0, p1 = (cx + sx * d, .56, cz + sz * d), (cx + sx * d, spring - .28, cz + sz * d)
                sk.cylinder(g, p0, p1, .07, F['shaft'], T.small, caps=False)
    for (sx, sz) in shafts:
        p0, p1 = (sx, .5 if T.i < 2 else .34, sz), (sx, spring - .62, sz)
        sk.cylinder(g, p0, p1, r, F['shaft'], T.round, caps=False)
        if T.fine:
            lathe(g, [(r + .04, 0), (r + .04, .06), (r + .025, .1), (r + .015, .14)], p0, F['trim'], T.round, cap1=True)
        elif T.i == 1:
            lathe(g, [(r + .04, 0), (r + .04, .06), (r + .01, .14)], p0, F['trim'], T.round)
        capital(g, (sx, spring, sz), r, T)
    # the pier's capital band over the core
    if T.fine:
        mould_round(g, octagon(cx, cz, h + .02, c, spring - .3), [(-.02, 0), (.05, 0), (.12, .14), (.22, .26), (.22, .34), (-.02, .34)], F['trim'], T, keep=(0, 1, 2, 3))
    elif T.i == 1:
        mould_round(g, octagon(cx, cz, h + .02, c, spring - .3), [(.03, 0), (.22, .26), (.22, .34)], F['trim'], T, keep=(0, 1))
    if T.i < 2:
        prism(g, octagon(cx, cz, h + .2, (h + .2) * .42, spring + .04), .12, F['trim'], T)
    else:
        prism(g, octagon(cx, cz, h + .2, (h + .2) * .42, spring - .26), .42, F['trim'], T)


def loft_column(g, b, T):
    x, z = b['pos'][0], b['pos'][2]
    top = b['pos'][1] + b['size'][1] / 2
    r = b['size'][0] / 2 - .06
    lathe(g, [(r + .1, 0), (r + .1, .18), (r + .06, .22), (r + .07, .28), (r + .02, .34)], (x, 0, z), F['trim'], 8, cap1=True, phase=math.pi / 8)
    lathe(g, [(r, .34), (r * .97, top - .45)], (x, 0, z), F['pier'], 8, phase=math.pi / 8)
    lathe(g, [(r, top - .45), (r * 1.15, top - .3), (r + .14, top - .12), (r + .14, top)], (x, 0, z), F['trim'], 8, cap1=True, phase=math.pi / 8)


# ---------------------------------------------------------------------------------------------------------------------------
# The vault
# ---------------------------------------------------------------------------------------------------------------------------
XLINES = (-30, -24, -18, -12, -6, 6, 12, 18, 24, 30)
ZBANDS = (-11.5, -6, 6, 11.5)


arch_h = sk.arch_height


class Bay:
    """A groin bay: two pointed barrels crossing (the ceiling is the higher of the two: the groins run corner to corner), domed by
    `dome` toward the crown; its edges' arches rise from the springing to crown - dome."""

    def __init__(self, x0, x1, z0, z1, crown, spring=13.0, oculus=0.0, dome=.5):
        self.x0, self.x1, self.z0, self.z1, self.crown, self.spring, self.oculus, self.dome = x0, x1, z0, z1, crown, spring, oculus, dome
        self.cx, self.cz, self.hx, self.hz = (x0 + x1) / 2, (z0 + z1) / 2, (x1 - x0) / 2, (z1 - z0) / 2
        self.edge = crown - dome - spring

    def y(self, x, z):
        u, v = (x - self.cx) / self.hx, (z - self.cz) / self.hz
        bz = arch_h(self.hx, self.edge, (1 - abs(u)) * self.hx)
        bx = arch_h(self.hz, self.edge, (1 - abs(v)) * self.hz)
        return self.spring + max(bx, bz) + self.dome * (1 - u * u) * (1 - v * v)

    def barrel(self, x, z, which):
        u, v = (x - self.cx) / self.hx, (z - self.cz) / self.hz
        b = arch_h(self.hx, self.edge, (1 - min(1, abs(u))) * self.hx) if which == 'u' else arch_h(self.hz, self.edge, (1 - min(1, abs(v))) * self.hz)
        return self.spring + b + self.dome * (1 - u * u) * (1 - v * v)

    def normal(self, x, z, which=None):
        """The web's normal (down, into the hall); `which` ('u' or 'v') takes one barrel's, so a groin stays crisp."""
        e = .015
        f = self.y if which is None else (lambda a, b: self.barrel(a, b, which))
        dx = (f(x + e, z) - f(x - e, z)) / (2 * e)
        dz = (f(x, z + e) - f(x, z - e)) / (2 * e)
        return sk._norm((dx, -1.0, dz))


def vault_bay(g, bay, T, n=None):
    """The bay's web, cut along its groins (corner to centre) into four cells so the crease lies on the cells' edges, each
    cell a grid from its wall edge toward the crown; the crossing's web in rings round its oculus, its spokes on the groins."""
    n = n or max(4, (T.arc * 5) // 8)
    W = F['web']

    def P(x, z, which=None):
        return ((x, bay.y(x, z), z), bay.normal(x, z, which))
    if bay.oculus <= 0:
        C = (bay.cx, bay.cz)
        corners = [(bay.x0, bay.z0), (bay.x1, bay.z0), (bay.x1, bay.z1), (bay.x0, bay.z1)]
        rows = max(2, (n * 2) // 5)
        for k in range(4):
            A, B = corners[k], corners[(k + 1) % 4]
            which = 'u' if k % 2 == 0 else 'v'
            grid = []
            for j in range(rows + 1):
                t = (j / rows) ** 1.15
                row = []
                cols = n
                for i in range(cols + 1):
                    s = .5 - .5 * math.cos(math.pi * i / cols)
                    ex, ez = A[0] + (B[0] - A[0]) * s, A[1] + (B[1] - A[1]) * s
                    row.append(P(ex + (C[0] - ex) * t, ez + (C[1] - ez) * t, which))
                grid.append(row)
            for j in range(rows):
                for i in range(n):
                    a, b, c, d = grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]
                    if j == rows - 1:
                        g.face((a[0], b[0], c[0]), W, normals=(a[1], b[1], c[1]))
                    else:
                        g.face((a[0], b[0], c[0], d[0]), W, normals=(a[1], b[1], c[1], d[1]))
    else:
        m = 8 * max(2, (n + 1) // 2)
        rings = max(4, n)
        pts = {}
        for k in range(m + 1):
            th = 2 * math.pi * k / m
            cs, sn = math.cos(th), math.sin(th)
            rmax = bay.hx / max(abs(cs), abs(sn))
            for j in range(rings + 1):
                t = j / rings
                r = bay.oculus + (rmax - bay.oculus) * (t ** 1.3)
                pts[k, j] = P(bay.cx + r * cs, bay.cz + r * sn)
        for k in range(m):
            for j in range(rings):
                a, b, c, d = pts[k, j], pts[k + 1, j], pts[k + 1, j + 1], pts[k, j + 1]
                g.face((a[0], b[0], c[0], d[0]), W, normals=(a[1], b[1], c[1], d[1]))


# (the ribs hang eleven metres and more over the floor: a keeled section of five corners reads as the moulded one did)
RIB = [(-.15, -.06), (.15, -.06), (.14, .14), (0, .38), (-.14, .14)]
RIB_E = [(-.13, -.06), (.13, -.06), (.12, .12), (0, .33), (-.12, .12)]
RIB_LOW = [(-.15, -.06), (.15, -.06), (0, .38)]


def rib(g, bay, p0, p1, T, section, f=None, steps=None):
    """A rib on the web from (x, z) p0 to p1 (plan), its section hanging under the surface."""
    n = steps or T.arch
    path, norms = [], []
    for i in range(n + 1):
        t = i / n
        x, z = p0[0] + (p1[0] - p0[0]) * t, p0[1] + (p1[1] - p0[1]) * t
        # inside the bay by a hair so the edge ribs take this bay's own surface
        xi = min(max(x, bay.x0 + 1e-3), bay.x1 - 1e-3)
        zi = min(max(z, bay.z0 + 1e-3), bay.z1 - 1e-3)
        y = bay.y(xi, zi)
        path.append((x, y, z))
        n_ = bay.normal(xi, zi)
        norms.append(n_)
    sweep(g, section, path, F['rib'] if f is None else f, path_normals=norms, smooth_u=True, caps=False)


def boss(g, p, T, r=.34, near=False):
    """A gilded boss under a vault's crown (near: the crypt's, a pilot's height away)."""
    lathe(g, [(0, 0), (r * .9, .02), (r, -.06), (r * .8, -.16), (r * .4, -.24), (0, -.26)], p, F['gold'], T.round if near else (8, 6, 5)[T.i], cap0=False)
    if T.fine or (near and T.detail):
        for i in range(8):
            a = 2 * math.pi * i / 8
            tip = (p[0] + math.cos(a) * r * 1.3, p[1] - .04, p[2] + math.sin(a) * r * 1.3)
            sk.tube(g, [(p[0] + math.cos(a) * r * .6, p[1] - .1, p[2] + math.sin(a) * r * .6), tip], .05, F['gold'], 4 if near and T.fine else 3, caps=False)


def vaults(g, L, T):
    bays = []
    for i in range(len(XLINES) - 1):
        x0, x1 = XLINES[i], XLINES[i + 1]
        if x0 == -6:
            continue
        for j in range(len(ZBANDS) - 1):
            bays.append(Bay(x0, x1, ZBANDS[j], ZBANDS[j + 1], 20.5 if j == 1 else 20.2))
    cross = Bay(-6, 6, -6, 6, 20.6, spring=12.0, oculus=2.4)
    bays.append(cross)
    for s in (-1, 1):
        bays.append(Bay(-6, 6, -11.5 if s < 0 else 6, -6 if s < 0 else 11.5, 20.2))
        bays.append(Bay(-6, 6, -20 if s < 0 else 11.5, -11.5 if s < 0 else 20, 20.3))
    edges = set()
    for bay in bays:
        vault_bay(g, bay, T)
        corners = [(bay.x0, bay.z0), (bay.x1, bay.z0), (bay.x1, bay.z1), (bay.x0, bay.z1)]
        for (ax, az) in corners:
            if bay.oculus > 0:
                dx, dz = bay.cx - ax, bay.cz - az
                l = math.hypot(dx, dz)
                rib(g, bay, (ax, az), (bay.cx - dx / l * bay.oculus * 1.05, bay.cz - dz / l * bay.oculus * 1.05), T, RIB if T.fine else RIB_LOW)
            else:
                rib(g, bay, (ax, az), (bay.cx, bay.cz), T, RIB if T.fine else RIB_LOW)
        for k in range(4):
            a, b = corners[k], corners[(k + 1) % 4]
            key = (round(min(a[0], b[0]), 2), round(min(a[1], b[1]), 2), round(max(a[0], b[0]), 2), round(max(a[1], b[1]), 2))
            if key in edges:
                continue
            edges.add(key)
            # (an even count: the arch's apex is a point of the path)
            rib(g, bay, a, b, T, RIB_E if T.fine else RIB_LOW, steps=(8, 6, 4)[T.i])
        if bay.oculus <= 0:
            boss(g, (bay.cx, bay.y(bay.cx, bay.cz) - .2, bay.cz), T)
    # the oculus: a moulded ring and its gilded inner roll, the shaft up to the belfry's floor
    n = T.arc * 3
    r = cross.oculus
    ring = [(math.cos(2 * math.pi * k / n) * r, cross.y(math.cos(2 * math.pi * k / n) * r + 1e-3, math.sin(2 * math.pi * k / n) * r) - .1, math.sin(2 * math.pi * k / n) * r) for k in range(n)]
    sweep(g, [(-.25, -.2), (.25, -.2), (.3, .1), (0, .45), (-.3, .1)], ring, F['rib'], closed=True, path_normals=[(0, -1, 0)] * n)
    ring2 = [(math.cos(2 * math.pi * k / n) * (r - .22), ring[k][1] - .25, math.sin(2 * math.pi * k / n) * (r - .22)) for k in range(n)]
    sweep(g, sk.circle_section(.08, T.small), ring2, F['gold'], closed=True)
    top = cross.y(r, 0)
    lathe(g, [(r, top - .3), (r, 21.6)], (0, 0, 0), F['web'], n, smooth=True)
    return bays


# ---------------------------------------------------------------------------------------------------------------------------
# Floors, galleries, balustrades, stairs
# ---------------------------------------------------------------------------------------------------------------------------
def ext(b):
    p, s = b['pos'], b['size']
    return p[0] - s[0] / 2, p[0] + s[0] / 2, p[1] - s[1] / 2, p[1] + s[1] / 2, p[2] - s[2] / 2, p[2] + s[2] / 2


def floors(g, blocks, T):
    for b in blocks:
        i = b['id']
        if i.startswith('sanctum-floor'):
            x0, x1, y0, y1, z0, z1 = ext(b)
            box(g, x0, x1, y0, y1, z0, z1, F['floor'], T, bevel=.0, cell=.75 if T.fine else None, skip=('-y',))
        elif i == 'sanctum-crypt-floor':
            x0, x1, y0, y1, z0, z1 = ext(b)
            box(g, x0, x1, y0, y1, z0, z1, F['cryptfloor'], T, bevel=.0, skip=('-y',))


def gallery(g, b, parts, T):
    x0, x1, y0, y1, z0, z1 = ext(b)
    box(g, x0, x1, y1 - .06, y1, z0, z1, F['gfloor'], T, bevel=0.0, skip=('-y',))
    box(g, x0, x1, y0, y1 - .06, z0, z1, F['soffit'], T, bevel=0.0, skip=('+y',))
    for p in parts:
        hx, hy, hz = p['h']
        c = p['c']
        if KIND[p['k']] == F['trim']:
            # a fascia: a cornice along its long axis, facing away from the gallery
            along_x = hx > hz
            if along_x:
                out = 1 if c[2] > (z0 + z1) / 2 else -1
                path = [(c[0] - hx, y0 - .02, c[2] - out * hz), (c[0] + hx, y0 - .02, c[2] - out * hz)]
            else:
                out = 1 if c[0] > (x0 + x1) / 2 else -1
                path = [(c[0] - out * hx, y0 - .02, c[2] - hz), (c[0] - out * hx, y0 - .02, c[2] + hz)]
            prof = [(-.02, -.08), (.04, -.08), (.07, -.02), (.06, .04), (.1, .1), (.14, .2), (.14, .3), (.1, .34), (.1, .44), (-.02, .44)]
            d = (path[1][0] - path[0][0], 0, path[1][2] - path[0][2])
            u = sk._norm(sk._cross(Y, d))
            want = (0, 0, out) if along_x else (out, 0, 0)
            if sk._dot(u, want) < 0:
                path = path[::-1]
            sweep(g, prof, path, F['trim'], path_normals=[Y, Y], smooth_u=False)
        else:
            # an oak beam under the soffit, chamfered, its ends carved
            # (its top is under the soffit; on the Low tier its ends are left out too)
            ends = ('-x', '+x') if hx > hz else ('-z', '+z')
            box(g, c[0] - hx, c[0] + hx, c[1] - hy, c[1] + hy, c[2] - hz, c[2] + hz, kind_fin(p['k'], 0 if hx > hz else 2), T, bevel=.03,
                cell=max(T.cell, 2.5), skip=('+y',) + (ends if T.i == 2 else ()))


OCC = Geo()       # what only shades (the bake's occluders): never in the bank


def balustrade(g, b, T):
    x0, x1, y0, y1, z0, z1 = ext(b)
    along_x = (x1 - x0) >= (z1 - z0)
    st, dk = F['trim'], F['dark']
    length = (x1 - x0) if along_x else (z1 - z0)
    n = max(1, int(round(length / .75)))
    step = length / n
    lo = x0 if along_x else z0
    ya, yb = y0 + .12, y1 - .08
    if not T.detail:
        box(g, x0, x1, ya, yb, z0, z1, F['stone'], T, bevel=.01, skip=('-y', '+y'))
    else:
        # the body: its long faces cut on the panels' lines, a column of corners behind each strip, where the bake's occlusion
        # is deepest (a solid strip, a little shallower, stands in the bake under the drawn one's front), so the panels read
        # dark and sunk between their strips; its ends plain
        for side in (-1, 1):
            face = (z0 if side < 0 else z1) if along_x else (x0 if side < 0 else x1)
            for i in range(n):
                for j in range(2):
                    u0, u1 = lo + step * i, lo + step * (i + 1)
                    v0, v1 = ya + (yb - ya) * j / 2, ya + (yb - ya) * (j + 1) / 2
                    q = [(u0, v0), (u1, v0), (u1, v1), (u0, v1)]
                    g.face([(u, v, face) if along_x else (face, v, u) for (u, v) in q], F['stone'], (0, 0, side) if along_x else (side, 0, 0))
            for i in range(1, n):
                u, (d0, d1) = lo + step * i, sorted((face - side * .01, face + side * .014))
                if along_x:
                    box(OCC, u - .025, u + .025, ya - .02, yb + .02, d0, d1, st, None, bevel=0, cell=0)
                else:
                    box(OCC, d0, d1, ya - .02, yb + .02, u - .025, u + .025, st, None, bevel=0, cell=0)
        for e in (-1, 1):
            u = lo if e < 0 else lo + length
            a, c = (z0, z1) if along_x else (x0, x1)
            g.face([(u, ya, a), (u, ya, c), (u, yb, c), (u, yb, a)] if along_x else [(a, ya, u), (c, ya, u), (c, yb, u), (a, yb, u)], F['stone'], (e, 0, 0) if along_x else (0, 0, e))
    # the base and the coping, moulded
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    if along_x:
        base = [(x0 - .04, y0, cz), (x1 + .04, y0, cz)]
    else:
        base = [(cx, y0, z0 - .04), (cx, y0, z1 + .04)]
    t = (z1 - z0) if along_x else (x1 - x0)
    hb = t / 2
    sweep(g, [(-hb - .04, 0), (hb + .04, 0), (hb + .04, .08), (hb + .01, .12), (-hb - .01, .12), (-hb - .04, .08)], base, st, path_normals=[Y, Y], smooth_u=False)
    top = [(p[0], y1 - .08, p[2]) for p in base]
    sweep(g, [(-hb - .05, 0), (hb + .05, 0), (hb + .06, .04), (hb + .03, .1), (hb - .02, .12), (-hb + .02, .12), (-hb - .03, .1), (-hb - .06, .04)], top, st, path_normals=[Y, Y], smooth_u=True)
    if not T.detail:
        return
    cy = (y0 + y1) / 2 + .02
    for i in range(n):
        u = lo + step * (i + .5)
        for side in (-1, 1):
            face = (z0 if side < 0 else z1) if along_x else (x0 if side < 0 else x1)

            def P(a, v, d=0.0):
                return (u + a, v, face + side * d) if along_x else (face + side * d, v, u + a)
            nrm = (0, 0, side) if along_x else (side, 0, 0)
            # a quatrefoil: four lobes, its field sunk dark, a raised fillet round it
            # (twenty corners, set evenly about each lobe's tip and on each notch, keep the lobes round)
            m = 20 if T.fine else 8
            pts = []
            for k in range(m):
                a = 2 * math.pi * (k + .5 if T.fine else k) / m
                r = .1 + .08 * abs(math.cos(2 * a)) ** .6
                pts.append((r * math.cos(a + math.pi / 4), r * math.sin(a + math.pi / 4)))
            mid = P(0, cy, .003)
            for k in range(m):
                a0, a1 = pts[k], pts[(k + 1) % m]
                g.face((mid, P(a0[0], cy + a0[1], .003), P(a1[0], cy + a1[1], .003)), dk, nrm)
                if T.tiny:
                    g.face((P(a0[0], cy + a0[1], .012), P(a1[0], cy + a1[1], .012), P(a1[0] * 1.22, cy + a1[1] * 1.22, .012), P(a0[0] * 1.22, cy + a0[1] * 1.22, .012)), st, nrm)
            # the panel's frame between the lobes
            if i > 0 and T.fine:
                # (a strip proud of the face: its front; its ends die into the base and coping)
                a = -step / 2
                g.face((P(a - .025, ya, .02), P(a + .025, ya, .02), P(a + .025, yb, .02), P(a - .025, yb, .02)), st, nrm)


def flight(g, b, nose, T):
    x0, x1, y0, y1, z0, z1 = ext(b)
    box(g, x0, x1, y0, y1, z0, z1, F['stone'], T, bevel=.012, cell=max(T.cell, 2.0), skip=('-y',))
    if nose is not None:
        c, h = nose['c'], nose['h']
        along_x = h[0] > h[2]
        if along_x:
            path = [(c[0] - h[0], c[1], c[2]), (c[0] + h[0], c[1], c[2])]
        else:
            path = [(c[0], c[1], c[2] - h[2]), (c[0], c[1], c[2] + h[2])]
        sweep(g, sk.circle_section(.035, T.small), path, F['trim'])


def floor_marks(g, L, T, parts):
    """The runner, the brass rings round the chandeliers' zones, the crossing's star (rays as the kit's, its rings swept) and
    the grave slabs; parts the kit lays flat stay as finished slabs."""
    for h in L['hazards']:
        if h['kind'] == 'crane':
            for z in h['zones']:
                r = max(z['size'][0], z['size'][2]) / 2
                n = T.arc * 3
                ring = [(z['center'][0] + math.cos(2 * math.pi * k / n) * r, .012, z['center'][2] + math.sin(2 * math.pi * k / n) * r) for k in range(n)]
                sweep(g, sk.rect_section(.09, .03), ring, F['brass'], closed=True, path_normals=[Y] * n)
                ring = [(z['center'][0] + math.cos(2 * math.pi * k / n) * (r - .5), .008, z['center'][2] + math.sin(2 * math.pi * k / n) * (r - .5)) for k in range(n)]
                sweep(g, sk.rect_section(.05, .02), ring, F['brass'], closed=True, path_normals=[Y] * n)
    for rr, w in ((1.3, .08), (4.6, .1)):
        n = T.arc * 4
        ring = [(math.cos(2 * math.pi * k / n) * rr, .012, math.sin(2 * math.pi * k / n) * rr) for k in range(n)]
        sweep(g, sk.rect_section(w, .026), ring, F['brass'], closed=True, path_normals=[Y] * n)


# ---------------------------------------------------------------------------------------------------------------------------
# Candles
# ---------------------------------------------------------------------------------------------------------------------------
def candle(g, x, y, z, r, tall, T, seed):
    """A wax candle standing at (x, y, z): its body a little uneven, its top melted into a cup with a raised rim, drips
    run down its side, the wick black above."""
    rnd = np.random.default_rng(seed)
    # (a candle is five centimetres across and there are five hundred: six sides, its melted rim and cup, one drip, the wick)
    if not T.fine:
        lathe(g, [(r, 0.0), (r * .9, tall)], (x, y, z), F['wax'], 3, cap0=False, cap1=True, phase=float(rnd.uniform(0, 6.28)))
        return
    lathe(g, [(r * 1.02, 0.0), (r, tall - .012), (r * .9, tall), (r * .35, tall - .012)], (x, y, z), F['wax'], 6, cap0=False, phase=float(rnd.uniform(0, 6.28)))
    if T.tiny:
        a = float(rnd.uniform(0, 2 * math.pi))
        length = float(rnd.uniform(.25, .7)) * tall
        top = tall - .004
        p0 = (x + math.cos(a) * r * .95, y + top, z + math.sin(a) * r * .95)
        p1 = (x + math.cos(a) * r * 1.02, y + top - length * .5, z + math.sin(a) * r * 1.02)
        p2 = (x + math.cos(a) * r * 1.0, y + top - length, z + math.sin(a) * r * 1.0)
        sk.sweep(g, sk.circle_section(.009, 3), [p0, p1, p2], F['wax'], scale=[1.0, 1.15, 1.5], caps=False)
        # the wick
        lathe(g, [(.0035, 0.0), (.0025, .026)], (x, y + tall - .014, z), F['wick'], 3)


# ---------------------------------------------------------------------------------------------------------------------------
# The kit's other parts, rebuilt as finished pieces
# ---------------------------------------------------------------------------------------------------------------------------
BASE = [(-.02, 0.0), (.06, 0.0), (.06, .08), (.035, .11), (.045, .14), (.01, .17), (-.02, .17)]
CAP = [(-.02, 0.0), (.0, 0.0), (.045, .035), (.07, .075), (.07, .12), (-.02, .12)]


def moulded(g, b, f, T, panels=True, trim=None):
    """A block's body with a moulded base and cornice swept round it and fielded panels on its faces."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    tr = F['trim'] if trim is None else trim
    if T.i == 2:
        box(g, x0, x1, y0, y1, z0, z1, f, T, bevel=0, skip=('-y',))
        return
    # (the body keeps the finer cells: the bake's shade between its panels is its dark stiles)
    box(g, x0, x1, y0 + .14, y1 - .1, z0, z1, f, T, bevel=.015, cell=.75 if T.fine else None, skip=('-y', '+y'))
    rect = lambda y: [(x0, y, z0), (x1, y, z0), (x1, y, z1), (x0, y, z1)]
    # (the mouldings without their feet and backs; two and three faces on the Mid tier)
    if T.fine:
        mould_round(g, rect(y0), BASE, tr, T, keep=(1, 2, 3, 4, 5))
        mould_round(g, rect(y1 - .12), CAP, tr, T, keep=(0, 1, 2, 3, 4))
    else:
        mould_round(g, rect(y0), [(.06, 0.0), (.06, .08), (.01, .17)], tr, T, keep=(0, 1))
        mould_round(g, rect(y1 - .12), [(0.0, 0.0), (.07, .075), (.07, .12), (-.02, .12)], tr, T, keep=(0, 1, 2))
    box(g, x0 + .01, x1 - .01, y1 - .02, y1, z0 + .01, z1 - .01, tr, T, bevel=.01, skip=('-y',))
    if not panels or not T.detail:
        return
    for (axis, lo, hi, at, sgn) in (('x', z0, z1, x0, -1), ('x', z0, z1, x1, 1), ('z', x0, x1, z0, -1), ('z', x0, x1, z1, 1)):
        span = hi - lo
        n = max(1, int(round(span / .9)))
        ya, yb = y0 + .3, y1 - .3
        if yb - ya < .25:
            continue
        for i in range(n):
            a, c = lo + span * i / n + .1, lo + span * (i + 1) / n - .1
            if c - a < .15:
                continue
            # a fielded panel: proud of the face, chamfered (its back left out); its front alone on the Mid tier
            if axis == 'x':
                if T.fine:
                    box(g, at, at + sgn * .02, ya, yb, a, c, tr, T, bevel=.012, cell=0, skip=('-x' if sgn > 0 else '+x',))
                else:
                    g.face(((at + sgn * .02, ya, a), (at + sgn * .02, ya, c), (at + sgn * .02, yb, c), (at + sgn * .02, yb, a)), tr, (sgn, 0, 0))
            else:
                if T.fine:
                    box(g, a, c, ya, yb, at, at + sgn * .02, tr, T, bevel=.012, cell=0, skip=('-z' if sgn > 0 else '+z',))
                else:
                    g.face(((a, ya, at + sgn * .02), (c, ya, at + sgn * .02), (c, yb, at + sgn * .02), (a, yb, at + sgn * .02)), tr, (0, 0, sgn))


def figure(g, p, T):
    """A carved robed figure in a kit statue's box (its thinner horizontal side is the relief's depth)."""
    c, (hx, hy, hz) = p['c'], p['h']
    wide, thin = (hx, hz) if hx >= hz else (hz, hx)
    k = (thin / wide) if wide > 0 else 1
    y0 = c[1] - hy
    H = 2 * hy
    prof = [(wide * .98, 0.0), (wide * .9, H * .2), (wide * .78, H * .5), (wide * .72, H * .66), (wide * .86, H * .84), (wide * .7, H * .94), (wide * .25, H), (0.0, H + .002)]
    lathe(g, prof, (c[0], y0, c[2]), F['statue'], T.round, cap0=False, scale=(1, k) if hx >= hz else (k, 1))
    if T.detail:
        # the folds of the robe: shallow ridges down its front and back
        for i in range(5):
            a = (i - 2) * .32
            for side in (-1, 1):
                if hx >= hz:
                    px, pz = c[0] + math.sin(a) * wide * .8, c[2] + side * thin * .92
                else:
                    px, pz = c[0] + side * thin * .92, c[2] + math.sin(a) * wide * .8
                sk.cylinder(g, (px, y0 + .02, pz), (px, y0 + H * .62, pz), .02, F['statue'], 4, caps=False)


def head(g, p, T):
    c, (hx, hy, hz) = p['c'], p['h']
    r = min(hx, hz) * 1.05
    lathe(g, [(0, -hy), (r * .6, -hy * .85), (r, -hy * .2), (r * .95, hy * .4), (r * .6, hy * .85), (0, hy)], c, F['statue'], T.round, scale=(1, 1))


def screen_niches(g, b, T):
    """The west screen's statue niches (dark) and the reredos's gilded ones: each bay's panel under a pointed head, an arch
    moulding round it and a cusp in its head (the kit's stepped heads give way to these)."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    east = x0 > 0
    face = x0 if east else x1
    s = -1 if east else 1
    bays = 5
    bay = (z1 - z0) / bays
    fill = F['gold'] if east else F['niche']
    nrm = (s, 0, 0)
    for i in range(bays):
        zc = z0 + bay * (i + .5)
        L, spring, H = bay * .36, 2.45, .83
        arc = pointed_arch(L, H, (6, 4, 3)[T.i])
        outline = [(zc - L, .45), (zc - L, spring)] + [(zc - L + a, spring + h) for (a, h) in arc[1:]] + [(zc + L - a, spring + h) for (a, h) in reversed(arc[:-1])] + [(zc + L, .45)]

        def P(z, y, d=0.0):
            return (face + s * d, y, z)
        mid = P(zc, 1.6, .012)
        for k in range(len(outline)):
            a, c = outline[k], outline[(k + 1) % len(outline)]
            g.face((mid, P(a[0], a[1], .012), P(c[0], c[1], .012)), fill, nrm)
        path = [P(z + (.05 if z > zc + 1e-6 else -.05 if z < zc - 1e-6 else 0), y, .03) for (z, y) in outline]
        sweep(g, sk.circle_section(.04, (5, 4, 3)[T.i]), path, F['trim'])


def saint_halo(g, b, T):
    x0, x1, y0, y1, z0, z1 = ext(b)
    cz = (z0 + z1) / 2
    mid = math.copysign(9.1, cz)
    face = z0 if abs(z0 - mid) < abs(z1 - mid) else z1
    s = -1 if face == z0 else 1
    cu = (x0 + x1) / 2
    n = T.arc
    ring = [(cu + math.cos(2 * math.pi * k / n) * .3, 2.22 + math.sin(2 * math.pi * k / n) * .3, face + s * .02) for k in range(n)]
    sweep(g, sk.rect_section(.05, .025), ring, F['gold'], closed=True, path_normals=[(0, 0, s)] * n)
    if T.detail:
        for k in range(12):
            a = 2 * math.pi * k / 12
            sweep(g, sk.rect_section(.02, .012), [(cu + math.cos(a) * .34, 2.22 + math.sin(a) * .34, face + s * .02), (cu + math.cos(a) * .46, 2.22 + math.sin(a) * .46, face + s * .02)], F['gold'], caps=T.fine)


BLOCKS = {}


def front_of(p):
    """The direction a part on a wall faces: from its owner block's middle toward it, along the block's thin side."""
    b = BLOCKS.get(p.get('o'))
    if b is None:
        return None
    sx, sz = b['size'][0], b['size'][2]
    if sx >= sz:
        return (0.0, 0.0, 1.0 if p['c'][2] > b['pos'][2] else -1.0)
    return (1.0 if p['c'][0] > b['pos'][0] else -1.0, 0.0, 0.0)


def skull(g, p, T):
    """A skull on a crypt shelf: the cranium, the face under it, the dark sockets and nose."""
    c, (hx, hy, hz) = p['c'], p['h']
    fr = front_of(p) or (0.0, 0.0, 1.0)
    base = (c[0], c[1] - hy, c[2])
    w = min(hx, hz) * 1.0 if abs(fr[2]) > .5 else min(hx, hz)
    sc = (1.0, .9) if abs(fr[2]) > .5 else (.9, 1.0)
    cranium = [(0.0, .02), (w * .55, .025), (w * .9, .07), (w * 1.0, .12), (w * .85, .17), (w * .45, .2), (0.0, .205)]
    if T.i == 2:
        lathe(g, [cranium[0], cranium[3], cranium[6]], base, F['bone'], 5, scale=sc)
        return
    lathe(g, cranium if T.fine else [cranium[0], cranium[2], cranium[3], cranium[4], cranium[6]], base, F['bone'], (8, 6, 4)[T.i], scale=sc)
    jaw = sk._add(base, fr, w * .45)
    lathe(g, [(0.0, 0.0), (w * .5, .005), (w * .55, .04), (w * .35, .065), (0.0, .07)] if T.fine else [(0.0, 0.0), (w * .55, .02), (0.0, .07)], jaw, F['bone'], (6, 4, 3)[T.i])
    if T.detail:
        # the sockets and the nose: dark flat marks on the face
        def mark(c, rad, k):
            u = (fr[2], 0.0, -fr[0])
            g.face([sk._add(sk._add(c, u, rad * math.cos(2 * math.pi * j / k)), (0, 1, 0), rad * math.sin(2 * math.pi * j / k)) for j in range(k)], F['niche'], fr)
        for side in (-1, 1):
            u = (fr[2] * side, 0.0, -fr[0] * side)
            mark(sk._add(sk._add(sk._add(base, fr, w * .93 + .003), u, w * .4), (0, 1, 0), .095), .024, 6 if T.fine else 4)
        mark(sk._add(sk._add(base, fr, w * .98 + .003), (0, 1, 0), .06), .012, 3)


def crypt_niche(g, p, T):
    """A crypt wall's burial niche: a round-headed dark recess with a roll round it (the kit's flat panel)."""
    c, (hx, hy, hz) = p['c'], p['h']
    fr = front_of(p) or (0.0, 0.0, 1.0)
    along = (1.0, 0.0, 0.0) if abs(fr[2]) > .5 else (0.0, 0.0, 1.0)
    w = hx if abs(fr[2]) > .5 else hz
    y0, y1 = c[1] - hy, c[1] + hy
    spring = y1 - w
    face = sk._add(c, fr, -min(hx, hz) + .004)
    def P(u, y, d=0.0):
        q = sk._add(face, along, u)
        return (q[0] + fr[0] * d, y, q[2] + fr[2] * d)
    n = (6, 4, 3)[T.i]
    outline = [(-w, y0), (-w, spring)] + [(-w * math.cos(math.pi * k / n), spring + w * math.sin(math.pi * k / n)) for k in range(1, n)] + [(w, spring), (w, y0)]
    mid = P(0, (y0 + y1) / 2)
    for k in range(len(outline)):
        a, b = outline[k], outline[(k + 1) % len(outline)]
        g.face((mid, P(a[0], a[1]), P(b[0], b[1])), F['niche'], fr)
    sweep(g, sk.circle_section(.03, (5, 4, 3)[T.i]), [P(u * 1.06, y, .015) for (u, y) in outline], F['trim'])


def kit_part(g, p, T):
    hx, hy, hz = p['h']
    lo = min(hx, hy, hz)
    q = p['q']
    f = KIND[p['k']]
    if p['f'] == 'crypt-wall' and f == F['bone']:
        skull(g, p, T)
        return
    if p['f'] == 'crypt-wall' and f == F['dark'] and min(hx, hy, hz) <= .0105:
        crypt_niche(g, p, T)
        return
    if T.i == 2 and max(hx, hy, hz) < .08:
        return
    if T.i > 0 and lo <= .012 and f != F['velvet']:
        # a thin plate (an inlay, a leaf of gilding, a slab's face) from sixteen metres: its two faces, no edges
        k = (hx, hy, hz).index(lo)
        a, b = [(1, 2), (2, 0), (0, 1)][k]
        hh = (hx, hy, hz)
        for sgn in (-1, 1):
            pts = []
            for (sa_, sb_) in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
                v = [0.0, 0.0, 0.0]
                v[k], v[a], v[b] = sgn * lo, sa_ * hh[a], sb_ * hh[b]
                pts.append(sk._add(p['c'], rotate(q, tuple(v))))
            nrm = [0.0, 0.0, 0.0]
            nrm[k] = float(sgn)
            nrm = rotate(q, tuple(nrm))
            # (a plate lying on a floor shows its upper face only)
            if nrm[1] < -.9 and any(-.01 <= p['c'][1] - lo - fy < .06 for fy in (-3.6, 0.0, 4.0)):
                continue
            g.face(pts, f, nrm)
        return
    if f == F['statue'] and abs(q[3]) > .999:
        if hy >= .4 and max(hx, hz) >= .12:
            figure(g, p, T)
            return
        if .08 < hy < .25 and max(hx, hz) <= .21:
            head(g, p, T)
            return
    # a bar (a square section along its local z, long): round it
    if abs(hx - hy) < .006 and hz > 3 * hx and f not in (F['glow'], F['ember']):
        ax = rotate(q, (0, 0, hz))
        c = p['c']
        sides = T.small if hx < .06 else T.round
        sk.cylinder(g, sk._sub(c, ax), sk._add(c, ax), hx * 1.05, f, sides)
        return
    # a curtain (velvet, thin): pleated
    if f == F['velvet'] and lo < .03 and T.detail:
        pleated(g, p, T)
        return
    if sk.FINS[f].detail == sk.DETAIL_WOOD:
        local = (hx, hy, hz)
        axis_local = int(np.argmax(local))
        world = rotate(q, tuple(1.0 if i == axis_local else 0.0 for i in range(3)))
        f = kind_fin(p['k'], int(np.argmax(np.abs(world))))
    obox(g, p['c'], (hx, hy, hz), q, f, T, bevel=min(.02, lo * .35))


def rotate(q, v):
    x, y, z, w = q
    # v' = q v q*
    tx = 2 * (y * v[2] - z * v[1])
    ty = 2 * (z * v[0] - x * v[2])
    tz = 2 * (x * v[1] - y * v[0])
    return (v[0] + w * tx + (y * tz - z * ty), v[1] + w * ty + (z * tx - x * tz), v[2] + w * tz + (x * ty - y * tx))


def pleated(g, p, T):
    """A thin hanging cloth: pleats across its wider horizontal side."""
    hx, hy, hz = p['h']
    c = p['c']
    along_x = hx >= hz
    w = hx if along_x else hz
    n = max(6, int(w / .05))
    rows = 4 if T.fine else 2
    f = KIND[p['k']]
    for i in range(n):
        for j in range(rows):
            def P(ii, jj):
                t = -w + 2 * w * ii / n
                d = .025 * math.sin(ii * math.pi / 2) * (1 - .3 * jj / rows)
                yy = c[1] - hy + 2 * hy * jj / rows
                return (c[0] + t, yy, c[2] + d) if along_x else (c[0] + d, yy, c[2] + t)

            def N(ii):
                s = math.cos(ii * math.pi / 2) * .6
                return sk._norm((-s, 0, 1)) if along_x else sk._norm((1, 0, -s))
            a, b, cc, d = P(i, j), P(i + 1, j), P(i + 1, j + 1), P(i, j + 1)
            for sgn in (1, -1):
                n0, n1 = N(i), N(i + 1)
                if sgn < 0:
                    n0, n1 = (-n0[0], -n0[1], -n0[2]), (-n1[0], -n1[1], -n1[2])
                g.face((a, b, cc, d), f, normals=(n0, n1, n1, n0))


# ---------------------------------------------------------------------------------------------------------------------------
# The crypt
# ---------------------------------------------------------------------------------------------------------------------------
def crypt(g, blocks, T):
    for b in blocks:
        i = b['id']
        x0, x1, y0, y1, z0, z1 = ext(b)
        if i.startswith('sanctum-crypt-wall') or i.startswith('sanctum-crypt-stairwall'):
            box(g, x0, x1, y0, y1, z0, z1, F['cryptwall'], T, bevel=.02)
        elif i.startswith('sanctum-crypt-pillar'):
            x, z = (x0 + x1) / 2, (z0 + z1) / 2
            r = (x1 - x0) / 2 - .02
            lathe(g, [(r + .1, y0), (r + .1, y0 + .2), (r + .02, y0 + .3)], (x, 0, z), F['trim'], 8, cap1=True, phase=math.pi / 8)
            lathe(g, [(r * .9, y0 + .3), (r * .86, y1 - .5)], (x, 0, z), F['pier'], T.round)
            # a cushion capital under a square abacus
            lathe(g, [(r * .86, y1 - .5), (r * 1.0, y1 - .38), (r * 1.25, y1 - .2)], (x, 0, z), F['trim'], T.round)
            box(g, x - r - .16, x + r + .16, y1 - .2, y1, z - r - .16, z + r + .16, F['trim'], T, bevel=.02)
        elif i.startswith('sanctum-crypt-tomb'):
            moulded(g, b, F['stone'], T, panels=True)
        elif i == 'sanctum-crypt-shrine':
            shrine(g, b, T)
    # the low vault: shallow groin bays between the pillars and the walls, springing 2.2 m over the floor
    xs, zs = (-8, -4, 4, 8), (-4.6, -2.4, 2.4, 4.6)
    for i in range(3):
        for j in range(3):
            bay = Bay(xs[i], xs[i + 1], zs[j], zs[j + 1], -0.45, spring=-1.4, dome=.15)
            vault_bay(g, bay, T, n=(8, 5, 4)[T.i])
            for (a, bb) in (((bay.x0, bay.z0), (bay.x1, bay.z1)), ((bay.x1, bay.z0), (bay.x0, bay.z1))):
                rib(g, bay, a, bb, T, [(-.09, -.04), (.09, -.04), (.09, .08), (0, .16), (-.09, .08)], F['trim'], steps=(12, 8, 4)[T.i])
            boss(g, (bay.cx, bay.y(bay.cx, bay.cz) - .08, bay.cz), T, r=.16, near=True)
    return


def shrine(g, b, T):
    """The crypt's reliquary: a gilded casket like a little church on a moulded marble plinth, its shallow roof crested, finials
    at its corners (the roof keeps to the block's top, so a pilot on it stands at most .3 m over the ridge's fall)."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    G, M = F['gold'], F['marble']
    box(g, x0, x1, y0 + .14, y0 + .5, z0, z1, M, T, bevel=.02, skip=('-y',))
    mould_round(g, [(x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)], BASE, M, T)
    cx0, cx1, cz0, cz1 = x0 + .12, x1 - .12, z0 + .12, z1 - .12
    eave = y1 - .3
    box(g, cx0, cx1, y0 + .5, eave, cz0, cz1, F['velvet'], T, bevel=.01)
    mould_round(g, [(cx0, y0 + .5, cz0), (cx1, y0 + .5, cz0), (cx1, y0 + .5, cz1), (cx0, y0 + .5, cz1)], [(-.02, 0), (.05, 0), (.05, .06), (.02, .09), (-.02, .09)], G, T)
    mould_round(g, [(cx0, eave - .1, cz0), (cx1, eave - .1, cz0), (cx1, eave - .1, cz1), (cx0, eave - .1, cz1)], CAP, G, T)
    # the arcade round the casket: gilded shafts and round heads over velvet
    for (along, a0, a1, at, sgn) in (('x', cx0, cx1, cz0, -1), ('x', cx0, cx1, cz1, 1), ('z', cz0, cz1, cx0, -1), ('z', cz0, cz1, cx1, 1)):
        n = max(2, int(round((a1 - a0) / .55)))
        for k in range(n + 1):
            u = a0 + (a1 - a0) * k / n
            p0 = (u, y0 + .58, at + sgn * .03) if along == 'x' else (at + sgn * .03, y0 + .58, u)
            p1 = (p0[0], eave - .32, p0[2])
            sk.cylinder(g, p0, p1, .035, G, T.small)
        for k in range(n):
            u0, u1 = a0 + (a1 - a0) * k / n, a0 + (a1 - a0) * (k + 1) / n
            m = max(5, T.arc // 2)
            r = (u1 - u0) / 2
            pts = [((u0 + u1) / 2 - r * math.cos(math.pi * j / m), eave - .32 + r * .8 * math.sin(math.pi * j / m)) for j in range(m + 1)]
            path = [((uu, yy, at + sgn * .035) if along == 'x' else (at + sgn * .035, yy, uu)) for (uu, yy) in pts]
            sweep(g, sk.circle_section(.022, 5), path, G)
    # the relics glowing through a window on each long side
    for zz, sz in ((cz0, -1), (cz1, 1)):
        box(g, -.3, .3, y0 + .9, y0 + 1.5, zz, zz + sz * .012, F['glow'], None, bevel=0, cell=0)
        sweep(g, sk.circle_section(.025, 5), [(-.32, y0 + .88, zz + sz * .015), (.32, y0 + .88, zz + sz * .015), (.32, y0 + 1.52, zz + sz * .015), (-.32, y0 + 1.52, zz + sz * .015)], F['gold'], closed=True)
    # the roof: two shallow slopes from the eaves to the ridge along x, gold, a cresting of fleurons on the ridge
    rx0, rx1 = x0 + .04, x1 - .04
    for sz, zz in ((-1, z0 + .02), (1, z1 - .02)):
        nrm = sk._norm((0, 1, sz * .3 / max(.01, abs(zz))))
        g.face(((rx0, eave, zz), (rx1, eave, zz), (rx1, y1 - .02, 0.0), (rx0, y1 - .02, 0.0)), G, nrm)
    for sx, xx in ((-1, rx0), (1, rx1)):
        g.face(((xx, eave, z0 + .02), (xx, eave, z1 - .02), (xx, y1 - .02, 0.0)), G, (sx, 0, 0))
    g.face(((rx0, eave, z0 + .02), (rx1, eave, z0 + .02), (rx1, eave, z1 - .02), (rx0, eave, z1 - .02)), G, (0, -1, 0))
    sweep(g, sk.circle_section(.03, T.small), [(rx0, y1 - .02, 0.0), (rx1, y1 - .02, 0.0)], G)
    if T.detail:
        n = int((rx1 - rx0) / .25)
        for k in range(n + 1):
            x = rx0 + (rx1 - rx0) * k / n
            lathe(g, [(0, 0), (.035, .02), (.02, .06), (0, .1)], (x, y1 - .02, 0.0), G, 4)
    for sx in (x0 + .06, x1 - .06):
        for sz in (z0 + .06, z1 - .06):
            lathe(g, [(.05, 0), (.05, .08), (.03, .12), (.04, .16), (0, .3)], (sx, eave, sz), G, max(5, T.small))


def roof(g, blocks, T):
    for b in blocks:
        if b['id'].startswith('sanctum-vault'):
            x0, x1, y0, y1, z0, z1 = ext(b)
            box(g, x0, x1, y0, y1, z0, z1, F['web'], None, bevel=0, cell=0, skip=('-y',))


# ---------------------------------------------------------------------------------------------------------------------------
# The whole static scenery for one tier
# ---------------------------------------------------------------------------------------------------------------------------
SCULPTED = {'pier', 'wall', 'arcade', 'lancet', 'rose', 'vaults', 'balustrade', 'flight', 'pew', 'candle', 'loft-column', 'crypt-pillar', 'crypt-vault-ribs'}


def static_scene(L, T, pew_ids):
    g = Geo()
    global OCC
    OCC = Geo()
    blocks = L['blocks']
    BLOCKS.clear()
    BLOCKS.update({b['id']: b for b in blocks})
    walls(g, L, T)
    vaults(g, L, T)
    floors(g, blocks, T)
    roof(g, blocks, T)
    crypt(g, blocks, T)
    floor_marks(g, L, T, None)
    by_owner = {}
    for p in L['parts']:
        by_owner.setdefault(p.get('o'), []).append(p)
    for b in blocks:
        i = b['id']
        x0, x1, y0, y1, z0, z1 = ext(b)
        if i.startswith('sanctum-pier-cross'):
            pier(g, b, T, True)
        elif i.startswith('sanctum-pier'):
            pier(g, b, T, False)
        elif i.startswith('sanctum-column'):
            loft_column(g, b, T)
        elif i.startswith('sanctum-gallery'):
            gallery(g, b, [p for p in by_owner.get(i, []) if p['f'] == 'gallery'], T)
        elif i.startswith('sanctum-balustrade'):
            balustrade(g, b, T)
        elif i.startswith('sanctum-stair'):
            pass
        if i.startswith('sanctum-screen'):
            screen_niches(g, b, T)
        if i.startswith('sanctum-plinth'):
            saint_halo(g, b, T)
        if i.startswith('sanctum-screen') or i.startswith('sanctum-altar') or i.startswith('sanctum-confessional') or i.startswith('sanctum-plinth') \
                or i.startswith('sanctum-tomb'):
            key = i.split('-')[1]
            if key == 'confessional':
                box(g, x0, x1, y0, y1, z0, z1, F['oakdarky'], T, bevel=.03)
            elif key == 'altar':
                moulded(g, b, F['marble'], T, panels=True, trim=F['marble'])
            elif key == 'screen':
                moulded(g, b, F['stone'], T, panels=False)
            else:
                moulded(g, b, F['stone'], T, panels=key != 'tomb')
    # stairs with their nosings, and the rigs' fixed fittings
    for b in blocks:
        if b['id'].startswith('sanctum-stair'):
            flight(g, b, None, T)
    import sanctum_props as props
    props.fittings(g, L, T)
    for p in L['parts']:
        if p['f'] == 'flight':
            c, h = p['c'], p['h']
            along_x = h[0] > h[2]
            path = [(c[0] - h[0], c[1] + .005, c[2]), (c[0] + h[0], c[1] + .005, c[2])] if along_x else [(c[0], c[1] + .005, c[2] - h[2]), (c[0], c[1] + .005, c[2] + h[2])]
            sweep(g, sk.circle_section(.03, T.small), path, F['trim'], caps=T.fine)
    # candles: every kit candle as a sculpted one
    seed = 0
    for p in L['parts']:
        f = p['f']
        if f == 'candle':
            c, h = p['c'], p['h']
            seed += 1
            candle(g, c[0], c[1] - h[1], c[2], h[0], 2 * h[1], T, seed)
            continue
        if f in ('pier', 'wall', 'arcade', 'lancet', 'rose', 'vaults', 'balustrade', 'flight', 'pew', 'loft-column', 'crypt-pillar'):
            continue
        if f == 'gallery':
            continue
        if f == 'screen' and min(p['h']) <= .0105 and KIND[p['k']] in (F['dark'], F['gold']):
            continue
        if f == 'saint' and KIND[p['k']] == F['gold'] and max(p['h']) <= .041:
            continue
        if f == 'shrine':
            continue
        if f == 'crypt-vault' and p['k'] not in (KIND.index(F['glow']), KIND.index(F['chain'])) and p['r'] == 0:
            continue
        if f == 'floor-marks':
            hx, hy, hz = p['h']
            # the brass ring segments are swept above; the runner, the star's rays, slabs and channels stay as slabs
            if KIND[p['k']] == F['brass'] and abs(p['c'][1] - .02) < .005 and max(hx, hz) < .5 and hx < .07:
                continue
            if KIND[p['k']] == F['brass'] and abs(p['c'][1] - .016) < .003 and hx < .06:
                continue
        kit_part(g, p, T)
    return g


def soot_field(L):
    s = sk.SootField((-31, -4.5, -21), (31, 22, 21), cell=.2)
    for c in L['candles']:
        s.plume(c[0], c[1], c[2], .5, rise=1.3, width=.16)
    for l in L['lights']:
        s.plume(l[0], l[1], l[2], .25, rise=2.0, width=.4)
    return s


def floors_of(L):
    return (-3.6, 0.0, 4.0)


def moss_zone(W):
    """Moss grows in the crypt and low on the outer walls, little in the nave."""
    crypt = (W[:, 1] < -.3).astype(np.float64)
    wall = np.clip((np.maximum(np.abs(W[:, 0]) - 27, 0) + np.maximum(np.abs(W[:, 2]) - 10.5, 0)) / 1.5, 0, 1)
    return np.clip(crypt + wall * .6 + .1, 0, 1)


# ---------------------------------------------------------------------------------------------------------------------------
def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument('--layout', required=True)
    ap.add_argument('--out', help='Assets/LocalAssets to write the bank into')
    ap.add_argument('--preview', help='render views of the baked Fine tier')
    ap.add_argument('--tiers', default='0,1,2')
    ap.add_argument('--no-ao', action='store_true')
    ap.add_argument('--static-only', action='store_true')
    ap.add_argument('--own-mid', action='store_true', help="build the static scenery's Mid tier (about 5 MB more) instead of sharing the Fine one")
    args = ap.parse_args(argv)
    L = sk.load_layout(args.layout)
    finishes(L)
    import sanctum_props as props
    props.F, props.KIND, props.CANDLE = F, KIND, candle
    sb = sk.StageBank()
    soot = soot_field(L)
    pew_ids = [b['id'] for b in L['blocks'] if b['id'].startswith('sanctum-pew')]
    chunks = sk.Chunks(12.0, (-36.0, -24.0), (-1.0, 9.0))
    t0 = time.time()
    fine = {}
    for ti in [int(x) for x in args.tiers.split(',')]:
        T = Cut(ti)
        t = time.time()
        if ti == 1 and not args.own_mid:
            # the bank's budget: the static scenery has no Mid meshes of its own; each chunk's Mid prop is its Fine mesh again
            # (the Fine tier is cut to what reads at play distance and draws to the Mid reach)
            for cid in sorted(fine):
                sb.share('static/%d/1' % cid, 'static', fine[cid])
            print('tier mid: static shares the Fine meshes (%d chunks)' % len(fine))
            if not args.static_only:
                props.bank_props(sb, L, sk.Tier(ti), soot, floors_of(L), args.no_ao)
            continue
        g = static_scene(L, T, pew_ids)
        pews = props.pew_scene(L, T)
        print('tier %s: static %d faces, %.1f s' % (T.name, g.faces, time.time() - t))
        ao = None
        if not args.no_ao:
            ao = sk.bake_ao([g], distance=(1.4, 1.4, 1.6)[ti], samples=(32, 16, 12)[ti], occluders=[o for o in (pews, OCC) if o.faces])[0]
        bk = sk.Bake(g, ao, soot=soot, floors=floors_of(L), moss_zone=moss_zone)
        ids = chunks.ids(g)
        for cid in np.unique(ids):
            om = sk.to_mesh('static/%d/%d' % (cid, ti), g, bk, ids == cid)
            sb.add('static/%d/%d' % (cid, ti), 'static', om)
            if ti == 0 and om is not None:
                fine[int(cid)] = sb.mesh(om)
        if args.preview and ti == 0:
            views = [('nave-east', (-23.4, 1.9, -1.2), (10, 5, .5), 70), ('nave-west', (23.4, 2.0, 1.2), (-20, 6.5, 0), 70),
                     ('crossing-up', (-3.5, 1.6, -3.8), (.5, 16, 1.5), 75), ('aisle-south', (-26.5, 1.7, -9.0), (4, 2.6, -8.8), 70),
                     ('organ-loft', (-28.5, 5.8, 6.5), (2, 7, -1), 70), ('south-rose', (.4, 2.0, 7.5), (0, 9.5, -19), 70),
                     ('crypt', (-6.6, -2.1, 1.4), (4, -2.6, -1.4), 70), ('pier-close', (-15.6, 1.4, -1.2), (-18.3, 1.0, -5.0), 60),
                     ('window-close', (-21, 5.5, -9), (-21, 13, -11.6), 60), ('vault-close', (-15, 5, 0), (-15, 20, 3), 70),
                     ('saint', (-23.5, 1.6, -9.6), (-21, 1.4, -8.4), 60), ('screen-west', (-19.5, 1.7, 2.5), (-24.4, 1.6, -.5), 60),
                     ('shrine', (-3.2, -2.0, 2.6), (0, -2.6, 0), 60), ('altar', (-17.5, 1.6, 2.4), (-22, 1.1, 0), 55)]
            pbk = sk.Bake(pews, None)
            sk.preview([(g, bk), (pews, pbk)], views, args.preview)
        if not args.static_only:
            props.bank_props(sb, L, sk.Tier(ti), soot, floors_of(L), args.no_ao)
        del g
    if args.out:
        for b in L['blocks']:
            if b['role'] == 'static':
                sb.cover(b['id'])
        for i in pew_ids:
            sb.cover(i)
        sb.write(args.out, BANK)
    print('done in %.1f s' % (time.time() - t0))


if __name__ == '__main__':
    main()
