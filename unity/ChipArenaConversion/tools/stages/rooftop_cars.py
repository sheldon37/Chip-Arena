"""The menu rooftop's flying cars (docs/MENU-ROOFTOP-ART.md): hover vehicles in place of Core's two boxes, each in its own
frame: the origin at the lane's point (Core's box centre), +z the way it flies, +y up, about 2.3 m wide and 4.6 m long, so
Core's head and tail light sprites (2.3 m ahead and behind) sit on its lamps. Runtime NativeRooftopModels draws them
instanced along the lanes."""
import math

import stagekit as sk
import cybercity_kit as ck
import rooftop_kit as rk
from rooftop_kit import F, glow, box, lathe

KINDS = ('cab', 'saloon', 'coupe', 'van', 'hauler')


def _lights(t, T, x0, x1, hw, yl, tail=rk.MAGENTA):
    """Head and tail light bars, the side strips and the lift pods' glow under the body."""
    box(t, x1 - .03, x1 + .03, yl - .05, yl + .05, -hw * .78, hw * .78, glow(rk.ICE, 2.4), None, 0, cell=0)
    box(t, x0 - .03, x0 + .03, yl - .05, yl + .07, -hw * .8, hw * .8, glow(tail, 2.2), None, 0, cell=0)
    for s in (-1, 1):
        z = s * (hw + .005)
        box(t, x0 + .5, x1 - .7, yl - .12, yl - .07, min(z, z + s * .02), max(z, z + s * .02), glow(rk.CYAN, 1.8), None, 0, cell=0)
    n = (10, 6, 4)[T.i]
    for sx in (x0 + .85, x1 - .95):
        for s in (-1, 1):
            c = (sx, -.36, s * (hw - .32))
            if T.i < 2:
                lathe(t, [(.3, .06), (.33, 0), (.27, -.05)], c, F['carunder'], n)
            ck.disc(t, (c[0], c[1] - .052 if T.i < 2 else -.352, c[2]), .25, glow(rk.CYAN, 1.5), n, (0, -1, 0))


def car(T, kind):
    """One vehicle, built along +x and turned to fly along +z."""
    t = sk.Geo()
    n = (14, 10, 8)[T.i]
    seg = 2 if T.i == 0 else 1
    if kind in ('cab', 'saloon', 'coupe'):
        paint = F[{'cab': 'caryellow', 'saloon': 'carwhite', 'coupe': 'carred'}[kind]]
        low = kind == 'coupe'
        hw = 1.12 if not low else 1.15
        top = .32 if not low else .22
        # the hull: superellipse sections from the tail to the nose
        st = [(-2.3, -.2, top * .55, hw * .62), (-2.05, -.33, top * .95, hw * .9), (-1.2, -.35, top, hw), (.6, -.35, top, hw),
              (1.6, -.34, top * .8, hw * .92), (2.15, -.28, top * .35, hw * .7), (2.3, -.2, top * .1, hw * .4)]
        rings = [ck.section_yz(x, y0, y1, w, n, 3.2) for (x, y0, y1, w) in st]
        ck.skin(t, rings, paint)
        # the canopy: smoked glass over the cabin, a painted roof on the cab and the saloon
        ch = .86 if not low else .62
        cs = [(-1.35, top - .06, top + .02, hw * .5), (-1.0, top - .08, ch * .92, hw * .7), (-.2, top - .08, ch, hw * .74), (.5, top - .08, ch * .9, hw * .7),
              (1.15, top - .08, top + .04, hw * .52)]
        ck.skin(t, [ck.section_yz(x, y0, y1, w, n, 2.6) for (x, y0, y1, w) in cs], F['carglass'])
        if not low and T.i < 2:
            box(t, -.85, .3, ch - .03, ch + .012, -hw * .5, hw * .5, paint, None, 0, cell=0, skip=('-y',))
        if kind == 'cab':
            ck.rbox(t, -.45, -.05, ch, ch + .16, -.3, .3, .05, glow(rk.AMBER, 1.8), 1)
            if T.i == 0:
                for k in range(8):
                    xa = -1.6 + k * .42
                    for s in (-1, 1):
                        z = s * (hw + .004)
                        box(t, xa, xa + .21, -.02, .07, min(z, z + s * .012), max(z, z + s * .012), F['carblack'], None, 0, cell=0)
        if low:
            # twin fins and a wide thruster
            for s in (-1, 1):
                box(t, -2.25, -1.5, top - .05, top + .42, s * hw * .72 - .03, s * hw * .72 + .03, F['carblack'], None, 0, cell=0)
            box(t, -2.2, -1.45, top + .36, top + .42, -hw * .72, hw * .72, F['carblack'], None, 0, cell=0)
            box(t, -2.34, -2.28, -.2, .05, -hw * .45, hw * .45, glow(rk.WARM, 2.6), None, 0, cell=0)
        _lights(t, T, -2.3, 2.3, hw * .96, -.05, rk.MAGENTA if kind != 'cab' else rk.RED)
    else:
        paint = F['carteal' if kind == 'van' else 'carblack']
        hw = 1.15
        ck.rbox(t, -2.28, 1.0, -.35, .85, -hw, hw, .16, paint, seg)
        ck.rbox(t, .8, 2.3, -.35, .5, -hw + .04, hw - .04, .2, paint, seg)
        box(t, 2.2, 2.32, -.02, .38, -hw * .72, hw * .72, F['carglass'], None, 0, cell=0)
        for s in (-1, 1):
            z = s * (hw - .03)
            box(t, 1.0, 2.0, .02, .4, min(z, z + s * .012), max(z, z + s * .012), F['carglass'], None, 0, cell=0)
            if T.i < 2:
                zz = s * (hw + .004)
                box(t, -2.0, .7, .25, .45, min(zz, zz + s * .012), max(zz, zz + s * .012), glow(rk.MAGENTA if kind == 'van' else rk.AMBER, 1.2), None, 0, cell=0)
        if kind == 'hauler' and T.i < 2:
            # a roof rack of light bars and a beacon
            for xa in (-1.6, -.4):
                box(t, xa, xa + .12, .85, .97, -hw * .8, hw * .8, F['carunder'], None, 0, cell=0)
            ck.rbox(t, .2, .5, .85, 1.0, -.15, .15, .04, glow(rk.AMBER, 2.2), 1)
        _lights(t, T, -2.3, 2.3, hw * .98, -.08, rk.RED)
    # +x -> +z
    return t.transformed(sk.affine(sk.quat_axis(0, 1, 0, -math.pi / 2)))
