"""Pass 1's street modules with their inside-out faces turned the right way out (found by audit.py's rays, 2026-11-19).
mkit's lathe faces look to the right of its profile's travel, so a profile listed downward turns a surface inside out:

- the swan-neck's and the pendant's enamel dish lips ran inward and down: their faces looked into the dish, and under
  back-face culling the dish showed a gap at its rim;
- the pendant's drop-rod canopy (the little bell under the rod) ran outward and down: it showed its inside;
- the bench's blanket chose each face's side by a guess that failed where it turns from the seat up the back: three faces
  looked into the bench (holes from the front). Its outside is now the drape's own left side, row by row.

(The Vale's rear window, the fourth, is mkit's loft: kit2.loft_outward.) `install()` puts these in place of pass 1's for this
process (lamps.swan_neck, lamps.pendant, furniture._blanket), so pass 1's own module list builds the fixed pieces; everything
else is pass 1's code unchanged.
"""
import math

import mkit as mk
from mkit import F, Fr, Module, add, lathe
import furniture
import lamps
import stagekit as sk


def swan_neck(T):
    mk.finishes()
    m = Module('lamp.wall.swan-neck', T.lod, mount='wall', rest=(0.0, 4.3, 0.0), block='a facade of 9 m or more (plate at 4.0-4.6 m)',
               cap=(450, 60), note='arm 0.82 m from the wall')
    body, lamp = m.part('body'), m.part('lamp')
    green = F['iron-green']
    fr = Fr((0, 0, 0), (0, 0, 1))
    if T.lod == 0:
        lamps._plate(body, T, fr, lamps._stadium(.085, .17, 5), 0.0, .03, green)
        for b in (.2, -.2):
            mk.rod(body, fr.P(0, b, .03), fr.P(0, b, .045), .016, F['iron-black'], 4, caps=False)
    else:
        mk.fbox(body, T, fr, -.085, .085, -.25, .25, 0.0, .03, green, skip=('-z',))
    ctrl = [(0.03, -0.05), (0.17, -0.03), (0.33, 0.09), (0.47, 0.27), (0.63, 0.33), (0.77, 0.25), (0.82, 0.08), (0.82, -0.02)]
    path = mk.catmull([(0.0, y, z) for (z, y) in ctrl], T.pick(2, 1))
    if T.lod > 0:
        path = [path[0], path[3], path[5], path[-1]]
    mk.tube(body, path, .032, green, T.pick(5, 4), caps=T.lod == 0, radii=[.032 - .008 * i / (len(path) - 1) for i in range(len(path))])
    if T.lod == 0:
        sc = mk.catmull([(0.0, -.17, .03), (0.0, -.14, .13), (0.0, -.07, .22), (0.0, .02, .29), (0.0, .07, .34)], 2)
        mk.bar(body, sc, .016, .03, green, up=(1, 0, 0))
        mk.ellipsoid(body, (0.0, .33, .64), ((0, 0, 1), (1, 0, 0), (0, 1, 0)), (.03, .03, .03), F['brass'], 6, 2, top=1.0, bottom=-1.0)
    sx = (0.0, -0.02, .82)
    n = T.pick(10, 6)
    lathe(body, [(.24, -.16), (.215, -.145), (.05, -.07), (.025, -.05)] if T.lod == 0 else [(.24, -.16), (.03, -.05)], sx, F['enamel-green'], n)
    if T.lod == 0:
        lathe(body, [(.232, -.172), (.24, -.16)], sx, F['enamel-green'], n)          # FIX: the lip runs up and out, so it faces out
    if T.lod == 0:
        lathe(lamp, [(0.0, -.088), (.232, -.172)], sx, mk.glow(0xFFE2B8, 1.2), n)
        mk.rod(body, add(sx, (0, -.09, 0)), add(sx, (0, -.13, 0)), .025, F['iron-black'], 6, caps=False)
    else:
        mk.disc(lamp, add(sx, (0, -.155, 0)), .23, mk.glow(0xFFE2B8, 1.2), 6, (0, -1, 0))
    g_c = add(sx, (0, -.19, 0))
    mk.ellipsoid(lamp, g_c, ((1, 0, 0), (0, 0, 1), (0, 1, 0)), (.062, .062, .075), mk.glow(mk.SODIUM_GLASS, 3.0), T.pick(8, 5),
                 T.pick(4, 2), top=1.0, bottom=-1.0)
    m.light(g_c, mk.SODIUM_LAMP, 9.0)
    return m


def pendant(T):
    mk.finishes()
    m = Module('lamp.span.pendant', T.lod, mount='span', rest=(0.0, 7.1, 0.0), block='span wires between facades (lamp at 6.5 m)',
               cap=(400, 50), note='wires: lamp.span.wire, 32 / 8 a span')
    body, lamp = m.part('body'), m.part('lamp')
    n = T.pick(10, 6)
    iron, green = F['iron-black'], F['enamel-green']
    if T.lod == 0:
        lathe(body, [(.04, -.08), (.045, -.06), (.045, .02), (.03, .045), (.001, .06)], (0, 0, 0), iron, 8)
        for s in (-1, 1):
            mk.box(body, None, s * .045, s * .085, -.012, .012, -.012, .012, iron, 0.0)
            mk.tube(body, [(s * .085, -.018, 0), (s * .1, 0, 0), (s * .085, .018, 0)], .007, iron, 4, caps=False)
    else:
        mk.rod(body, (0, -.08, 0), (0, .05, 0), .04, iron, 4, caps=False)
    mk.rod(body, (0, -.08, 0), (0, -.33, 0), .018, iron, T.pick(6, 4), caps=False)
    if T.lod == 0:
        lathe(body, [(.075, -.375), (.06, -.355), (.02, -.33)], (0, 0, 0), iron, n)   # FIX: the canopy runs up and in, so it faces out
    rx = (0.0, 0.0, 0.0)
    lathe(body, [(.31, -.47), (.29, -.455), (.1, -.39), (.07, -.375)] if T.lod == 0 else [(.31, -.47), (.07, -.375)], rx, green, n)
    if T.lod == 0:
        lathe(body, [(.302, -.482), (.31, -.47)], rx, green, n)                        # FIX: the lip, as the swan-neck's
    lathe(lamp, [(0.0, -.41), (.08, -.415), (.302, -.482)] if T.lod == 0 else [(0.0, -.41), (.31, -.47)], rx, mk.glow(0xFFE2B8, 1.2), n)
    if T.lod == 0:
        mk.rod(body, (0, -.41, 0), (0, -.47, 0), .04, F['brass'], 8, caps=False)
    gc = (0.0, -.62, 0.0)
    mk.ellipsoid(lamp, gc, ((1, 0, 0), (0, 0, 1), (0, 1, 0)), (.15, .15, .155), mk.glow(mk.SODIUM_GLASS, 3.0), T.pick(8, 6), T.pick(6, 2),
                 top=1.0, bottom=-1.0)
    if T.lod == 0:
        mk.ellipsoid(body, (0, -.78, 0), ((1, 0, 0), (0, 0, 1), (0, 1, 0)), (.02, .02, .025), F['brass'], 6, 2, top=1.0, bottom=-1.0)
    m.light(gc, mk.SODIUM_LAMP, 12.0)
    return m


def _blanket(g, T):
    """Pass 1's blanket over the bench's back and seat, its faces turned by the drape's own side (the left of its run in
    (z, y), which is away from the bench all along)."""
    drape = [(-.36, .74), (-.33, .86), (-.27, .9), (-.21, .76), (-.17, .6), (-.13, .49), (-.02, .475), (.12, .47), (.25, .46), (.31, .4),
             (.33, .28)]
    if T.lod > 0:
        drape = drape[::2]
    nx = T.pick(8, 3)
    x0, x1 = -.62, .28
    rows = []
    for (z, y) in drape:
        row = []
        for i in range(nx):
            t = i / (nx - 1)
            x = x0 + (x1 - x0) * t
            fold = .018 * math.sin(t * 9.0 + z * 7.0) + .012 * math.sin(t * 23.0 + y * 5.0)
            off = .02 + fold
            nrm = (0.0, 1.0, 0.0) if y < .52 else sk._norm((0.0, .3, -1.0) if z < -.25 else (0.0, .4, 1.0))
            row.append((x, y + nrm[1] * off, z + nrm[2] * off))
        rows.append(row)

    for i in range(len(rows) - 1):
        # the drape's run (dz, dy) in (z, y); its left, (-dy, dz) there, is (0, dz, -dy) as a vector: away from the bench
        n = (0.0, drape[i + 1][0] - drape[i][0], -(drape[i + 1][1] - drape[i][1]))
        for j in range(nx - 1):
            g.face((rows[i][j], rows[i][j + 1], rows[i + 1][j + 1], rows[i + 1][j]), F['blanket'], n)
    if T.lod == 0:
        for i in (len(rows) - 2,):
            n = (0.0, (drape[i + 1][0] - drape[i][0]), -(drape[i + 1][1] - drape[i][1]))
            nn = sk._norm(n)
            for j in range(nx - 1):
                a, b, c, d = rows[i][j], rows[i][j + 1], rows[i + 1][j + 1], rows[i + 1][j]
                aa, bb = sk._lerp(a, d, .35), sk._lerp(b, c, .35)
                cc, dd = sk._lerp(b, c, .6), sk._lerp(a, d, .6)
                g.face(tuple(sk._add(p, nn, .004) for p in (aa, bb, cc, dd)), F['blanket-b'], n)


_ORIG = {}


def install():
    if _ORIG:
        return
    _ORIG.update(swan=lamps.swan_neck, pendant=lamps.pendant, blanket=furniture._blanket)
    lamps.swan_neck, lamps.pendant, furniture._blanket = swan_neck, pendant, _blanket


def uninstall():
    if not _ORIG:
        return
    lamps.swan_neck, lamps.pendant, furniture._blanket = _ORIG['swan'], _ORIG['pendant'], _ORIG['blanket']
    _ORIG.clear()
